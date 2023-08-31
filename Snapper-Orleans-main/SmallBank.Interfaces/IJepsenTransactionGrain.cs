using Orleans;
using Custom;
using System.Collections.Generic;
using System.Threading.Tasks;
using Concurrency.Interface;
using Utilities;

namespace SmallBank.Interfaces
{
    public interface IJepsenTransactionGrain : ITransactionExecutionGrain
    {
        Task<List<JepsenOperation>> Execute(List<JepsenOperation> operations, MyTransactionContext context);
        //Task<string> ParseAndExecute(string opstring);
        Task<TransactionResult> ParseAndExecute(MyTransactionContext context, object funcInput);
    }
}
