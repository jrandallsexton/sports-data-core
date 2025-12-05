# Azure Service Bus QuotaExceeded Error - Analysis & Fix

**Date**: December 4, 2025  
**Error**: `ServiceBusException: The maximum entity size has been reached or exceeded`

---

## ?? **The Problem**

### **Error Message:**
```
Azure.Messaging.ServiceBus.ServiceBusException: The maximum entity size has been reached or exceeded for Topic: 
'SB-PROD-SPORTDEETS:TOPIC:SPORTSDATA.CORE.EVENTING.EVENTS.DOCUMENTS/DOCUMENTCREATED'. 
Size of entity in bytes: 1073903350 (1.02 GB)
Max entity size in bytes:  1073741824 (1 GB)
```

### **What This Means:**
Your Azure Service Bus **topic** has accumulated **1GB+ of messages** that haven't been consumed/deleted yet.

**This is NOT about individual message size** - it's about **total accumulated messages** in the topic.

---

## ?? **Bug #1: Incorrect Size Calculation**

### **Original Code (Buggy):**
```csharp
public static int GetSizeInKilobytes(this string str)
{
    var byteCount = System.Text.Encoding.UTF8.GetByteCount(str);
    return byteCount / 1024;  // ? INTEGER DIVISION - LOSES PRECISION!
}

var jsonDoc = json.GetSizeInKilobytes() <= 200 ? json : null;
```

### **The Bug:**
**Integer division truncates decimals!**

Examples:
- 256.5 KB (262,656 bytes) ? `262656 / 1024 = 256` ? (should be 257)
- 300.8 KB (308,019 bytes) ? `308019 / 1024 = 300` ? (should be 301)

**Result**: Messages larger than 200 KB could incorrectly be sent inline.

---

## ? **Fix #1: Correct Size Calculation**

### **New Code:**
```csharp
/// <summary>
/// Gets the size of the string in kilobytes (KB) using UTF-8 encoding.
/// Uses ceiling to ensure size is not under-reported due to integer truncation.
/// </summary>
public static int GetSizeInKilobytes(this string str)
{
    if (string.IsNullOrEmpty(str))
        return 0;

    var byteCount = System.Text.Encoding.UTF8.GetByteCount(str);
    return (int)Math.Ceiling(byteCount / 1024.0); // ? Use Math.Ceiling
}

/// <summary>
/// Gets the exact size of the string in bytes using UTF-8 encoding.
/// </summary>
public static int GetSizeInBytes(this string str)
{
    if (string.IsNullOrEmpty(str))
        return 0;

    return System.Text.Encoding.UTF8.GetByteCount(str);
}
```

### **Updated Usage:**
```csharp
// Azure Service Bus limits:
// - Standard tier: 256 KB max message size
// - Premium tier: 1 MB max message size
const int MAX_INLINE_JSON_BYTES = 204_800; // 200 KB in bytes

var jsonSizeInBytes = json.GetSizeInBytes();
var jsonDoc = jsonSizeInBytes <= MAX_INLINE_JSON_BYTES ? json : null;

if (jsonDoc == null)
{
    _logger.LogInformation(
        "Document JSON size ({SizeKB} KB) exceeds {MaxKB} KB limit, sending reference only",
        jsonSizeInBytes / 1024.0,
        MAX_INLINE_JSON_BYTES / 1024);
}
```

**Key Changes:**
1. ? Use `GetSizeInBytes()` for exact byte count (no precision loss)
2. ? Define limit as bytes (`204_800`) instead of KB (`200`)
3. ? Added logging when JSON is too large

---

## ?? **Problem #2: Service Bus Topic Quota Exceeded**

### **Root Cause:**
Your Service Bus topic has **1GB+ of unconsumed messages**.

**Why this happens:**
1. **Slow consumers**: Your `Producer` service isn't processing messages fast enough
2. **Dead consumers**: A consumer crashed and stopped processing
3. **Message TTL**: Messages aren't expiring/being deleted after processing
4. **Backlog**: You have a huge backlog of `DocumentCreated` events

---

## ?? **Fix #2: Clear the Backlog**

### **Option 1: Drain the Topic (Recommended)**

**Azure Portal:**
1. Go to your Service Bus namespace: `sb-prod-sportdeets`
2. Navigate to **Topics** ? `sportsdata.core.eventing.events.documents/documentcreated`
3. Click **Service Bus Explorer**
4. **Peek** messages to see what's stuck
5. **Receive and Delete** messages in batches until topic is clear

**PowerShell (Faster):**
```powershell
# Install Azure Service Bus SDK if needed
Install-Module -Name Az.ServiceBus

# Connect
Connect-AzAccount

# Get topic details
$namespace = "sb-prod-sportdeets"
$topicName = "sportsdata.core.eventing.events.documents-documentcreated"
$resourceGroup = "<your-resource-group>"

Get-AzServiceBusTopic -ResourceGroupName $resourceGroup `
    -NamespaceName $namespace `
    -TopicName $topicName | 
    Select-Object Name, SizeInBytes, MessageCount
```

### **Option 2: Increase Topic Quota (Temporary Fix)**

**Azure Portal:**
1. Go to topic settings
2. Increase **Max size** from 1 GB to 5 GB (if available in your tier)
3. This is a **band-aid** - still need to fix consumer issues

---

## ?? **Diagnosis: Why Are Messages Accumulating?**

### **Check Consumer Health:**

1. **Is Producer service running?**
   ```powershell
   kubectl get pods -n <namespace> | grep producer
   ```

2. **Check consumer logs for errors:**
   ```powershell
   kubectl logs -n <namespace> <producer-pod> --tail=100 | grep ERROR
   ```

3. **Check subscription stats in Azure Portal:**
   - Go to Topic ? Subscriptions
   - Look for **Active Message Count**
   - Look for **Dead Letter Count**

4. **Check message age:**
   - Old messages (hours/days old) indicate stuck processing
   - Recent messages indicate high throughput

---

## ?? **Prevention: Long-Term Fixes**

### **1. Add Message TTL (Time To Live)**

Set a TTL on the topic so old messages auto-expire:

```csharp
// In your Service Bus configuration
services.AddMassTransit(x =>
{
    x.AddServiceBusMessageScheduler();
    
    x.UsingAzureServiceBus((context, cfg) =>
    {
        cfg.Host(connectionString);
        
        cfg.Message<DocumentCreated>(x =>
        {
            x.SetEntityName("sportsdata.core.eventing.events.documents/documentcreated");
            x.DefaultMessageTimeToLive = TimeSpan.FromHours(24); // ? Add TTL
        });
        
        cfg.ConfigureEndpoints(context);
    });
});
```

### **2. Implement Dead Letter Queue Monitoring**

```csharp
// Monitor dead letter queue
cfg.ReceiveEndpoint("documentcreated-dlq", e =>
{
    e.ConfigureDeadLetterQueueDeadLetterTransport();
    e.ConfigureDeadLetterQueueErrorTransport();
    
    e.Consumer<DeadLetterConsumer>(); // Custom handler to log/alert
});
```

### **3. Add Circuit Breaker for Publishing**

```csharp
public async Task Publish<T>(T message, CancellationToken ct) where T : class
{
    try
    {
        await _publishEndpoint.Publish(message, ct);
    }
    catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.QuotaExceeded)
    {
        _logger.LogError(ex, "Service Bus quota exceeded - message not published");
        
        // Option 1: Store message locally for retry
        await _failureStore.StoreForRetry(message);
        
        // Option 2: Alert operations team
        await _alertService.NotifyQuotaExceeded();
        
        // Don't throw - prevents cascading failures
    }
}
```

### **4. Monitor Topic Size**

Add Application Insights metric:

```csharp
_telemetryClient.GetMetric("ServiceBus.TopicSize")
    .TrackValue(topicSizeInBytes);

// Alert when > 800 MB (80% of 1 GB limit)
```

---

## ?? **Verification Commands**

### **Check Topic Size:**
```bash
# Using Azure CLI
az servicebus topic show \
  --resource-group <rg> \
  --namespace-name sb-prod-sportdeets \
  --name sportsdata.core.eventing.events.documents-documentcreated \
  --query '{size:sizeInBytes, messageCount:messageCount}'
```

### **Monitor Consumer Lag:**
```bash
# Check subscription backlog
az servicebus topic subscription show \
  --resource-group <rg> \
  --namespace-name sb-prod-sportdeets \
  --topic-name sportsdata.core.eventing.events.documents-documentcreated \
  --name producer-consumer \
  --query '{active:messageCount, deadLetter:deadLetterMessageCount}'
```

---

## ?? **Action Plan**

### **Immediate (Today):**
1. ? **Code fix deployed** (GetSizeInBytes correction)
2. ? **Drain topic backlog** via Service Bus Explorer
3. ? **Verify Producer service is running** and processing messages

### **Short-term (This Week):**
1. Add **message TTL** (24 hours)
2. Implement **dead letter queue monitoring**
3. Add **circuit breaker** for quota exceeded errors

### **Long-term (Next Sprint):**
1. Add **Application Insights metrics** for topic size
2. Set up **Azure Monitor alerts** for quota warnings (>80%)
3. Implement **exponential backoff** for message publishing
4. Consider **upgrading to Premium tier** for higher quotas

---

## ?? **References**

- [Azure Service Bus Quotas](https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-quotas)
- [Service Bus Exception Troubleshooting](https://aka.ms/azsdk/net/servicebus/exceptions/troubleshoot)
- [MassTransit Azure Service Bus Configuration](https://masstransit.io/documentation/transports/azure-service-bus)

---

**Status**: ? Code fix applied, awaiting backlog clearance
