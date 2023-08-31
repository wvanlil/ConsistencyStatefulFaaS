using System.Collections.Generic;
using System.Threading.Tasks;
using Concurrency.Interface;
using Orleans;
using Utilities;

namespace SmallBank.Interfaces
{
    public interface IDataGrain : ITransactionExecutionGrain
    {
        Task<TransactionResult> Init(MyTransactionContext context, object funcInput);
        Task<TransactionResult> Append(MyTransactionContext context, object funcInput); // int input
        Task<TransactionResult> Read(MyTransactionContext context, object funcInput); // nothing input
    }
}