# Quick Fix: Service Bus Quota Exceeded

## TL;DR

**Problem**: Service Bus topic has 1GB+ of unconsumed messages  
**Immediate Fix**: Drain the backlog via Azure Portal  
**Code Fix**: ? Applied (GetSizeInBytes correction)

---

## ?? **Immediate Action**

### **Azure Portal - Drain Messages:**

1. Go to: [Azure Portal](https://portal.azure.com)
2. Navigate to: **sb-prod-sportdeets** ? **Topics** ? `sportsdata.core.eventing.events.documents-documentcreated`
3. Click: **Service Bus Explorer**
4. Click: **Peek messages** (to see what's stuck)
5. Click: **Receive and Delete** ? Select **100 messages**
6. Repeat until topic size < 500 MB

**?? Warning**: This deletes unprocessed messages - only do this if you can re-process them later!

---

## ? **Code Fixes Applied**

### **1. Fixed GetSizeInKilobytes() Bug**
- **Before**: Integer division lost precision
- **After**: Uses `Math.Ceiling()` and new `GetSizeInBytes()` method

### **2. Updated Size Check Logic**
- **Before**: `json.GetSizeInKilobytes() <= 200`
- **After**: `json.GetSizeInBytes() <= 204_800` (exact byte count)

**Files Changed**:
- `src/SportsData.Core/Extensions/StringExtensions.cs`
- `src/SportsData.Provider/Application/Processors/ResourceIndexItemProcessor.cs`

---

## ?? **Check Consumer Health**

```bash
# Check if Producer service is running
kubectl get pods | grep producer

# Check consumer logs
kubectl logs <producer-pod> --tail=100 | grep ERROR

# Check topic stats
az servicebus topic show \
  --resource-group <rg> \
  --namespace-name sb-prod-sportdeets \
  --name <topic-name> \
  --query '{size:sizeInBytes, messageCount:messageCount}'
```

---

## ?? **Next Steps**

1. ? **Code deployed** (GetSizeInBytes fix)
2. ? **Drain backlog** (Azure Portal)
3. ? **Verify consumers running**
4. ? **Add message TTL** (24 hours)
5. ? **Add quota monitoring** (Application Insights)

---

**See full details**: `docs/troubleshooting/AZURE_SERVICE_BUS_QUOTA_EXCEEDED.md`
