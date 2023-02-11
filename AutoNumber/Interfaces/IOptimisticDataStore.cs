using System.Threading.Tasks;
using AutoNumber.Documents;

namespace AutoNumber.Interfaces;

public interface IOptimisticDataStore
{
    AutoNumberState GetAutoNumberState(string scopeName);
    
    Task<AutoNumberState> GetAutoNumberStateAsync(string scopeName);
    
    bool TryOptimisticWrite(AutoNumberState autoNumberState);
    
    Task<bool> TryOptimisticWriteAsync(AutoNumberState autoNumberState);
    
    Task<bool> InitializeAsync();
    
    bool Initialize();
}