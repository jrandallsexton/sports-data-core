using Azure;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Compute;
using Azure.ResourceManager.Compute.Models;

namespace SportsData.Producer.Infrastructure.Data;

public class AzureVmDatabaseScaler : IDatabaseScaler
{
    private readonly string _subscriptionId;
    private readonly string _resourceGroup;
    private readonly string _vmName;
    private readonly string _scaleUpSize;
    private readonly string _scaleDownSize;
    private readonly ILogger<AzureVmDatabaseScaler> _logger;

    public AzureVmDatabaseScaler(
        string subscriptionId,
        string resourceGroup,
        string vmName,
        string scaleUpSize,
        string scaleDownSize,
        ILogger<AzureVmDatabaseScaler> logger)
    {
        _subscriptionId = subscriptionId;
        _resourceGroup = resourceGroup;
        _vmName = vmName;
        _scaleUpSize = scaleUpSize;
        _scaleDownSize = scaleDownSize;
        _logger = logger;
    }

    public async Task ScaleUpAsync(string reason, CancellationToken cancellationToken = default)
    {
        await ResizeVmAsync(_scaleUpSize, reason, cancellationToken);
    }

    public async Task ScaleDownAsync(string reason, CancellationToken cancellationToken = default)
    {
        await ResizeVmAsync(_scaleDownSize, reason, cancellationToken);
    }

    private async Task ResizeVmAsync(string newSize, string reason, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Scaling VM '{Vm}' to '{Size}' due to: {Reason}", _vmName, newSize, reason);

        var armClient = new ArmClient(new DefaultAzureCredential());
        var vmResourceId = VirtualMachineResource.CreateResourceIdentifier(_subscriptionId, _resourceGroup, _vmName);
        var vm = armClient.GetVirtualMachineResource(vmResourceId);

        var currentVm = await vm.GetAsync(InstanceViewType.InstanceView, cancellationToken);
        var powerState = currentVm.Value.Data.InstanceView?.Statuses?
            .FirstOrDefault(s => s.Code.StartsWith("PowerState/"))?.Code;

        var currentSize = currentVm.Value.Data.HardwareProfile?.VmSize;

        if (currentSize == newSize)
        {
            _logger.LogInformation("VM '{Vm}' is already size '{Size}'. No resize needed.", _vmName, newSize);

            if (powerState != "PowerState/running")
            {
                _logger.LogInformation("VM '{Vm}' is not running. Powering on...", _vmName);
                await vm.PowerOnAsync(WaitUntil.Completed, cancellationToken);
            }

            return;
        }


        if (powerState == "PowerState/running")
        {
            _logger.LogInformation("VM is running. Powering off...");
            await vm.PowerOffAsync(WaitUntil.Completed, skipShutdown: false, cancellationToken);
        }
        else if (powerState == "PowerState/stopped")
        {
            _logger.LogInformation("VM is stopped but not deallocated. Deallocating now...");
            await vm.DeallocateAsync(WaitUntil.Completed, false, cancellationToken);
        }
        else
        {
            _logger.LogInformation("VM is in state '{State}'. Proceeding with resize.", powerState);
        }

        // Prepare and apply the resize operation
        var update = new VirtualMachinePatch
        {
            HardwareProfile = new VirtualMachineHardwareProfile
            {
                VmSize = newSize
            }
        };

        _logger.LogInformation("Applying resize to '{Size}'...", newSize);
        await vm.UpdateAsync(WaitUntil.Completed, update, cancellationToken);

        // Power the VM back on
        _logger.LogInformation("Powering on VM '{Vm}'...", _vmName);
        await vm.PowerOnAsync(WaitUntil.Completed, cancellationToken);

        _logger.LogInformation("VM '{Vm}' successfully resized to '{Size}'", _vmName, newSize);
    }
}