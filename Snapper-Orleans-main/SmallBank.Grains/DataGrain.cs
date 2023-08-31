using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using Orleans;
using Orleans.Transactions.Abstractions;
using Orleans.Concurrency;
using SmallBank.Interfaces;
using System.Collections.Generic;
using System;
using Concurrency.Implementation;
using Utilities;
using Persist.Interfaces;
using Custom;

namespace SmallBank.Grains
{
    [Serializable]
    public class DataStore : ICloneable
    {
        public List<int> list;

        public DataStore()
        {
            list = new List<int>();
        }

        object ICloneable.Clone()
        {
            var clonedData = new DataStore();
            clonedData.list.AddRange(list);
            return clonedData;
        }
    }

    [Reentrant]
    public class DataGrain : TransactionExecutionGrain<DataStore>, IDataGrain
    {
        public DataGrain(IPersistSingletonGroup persistSingletonGroup) : base(persistSingletonGroup, "SmallBank.Grains.DataGrain")
        {
        }

        public async Task<TransactionResult> Init(MyTransactionContext context, object funcInput)
        {
            TransactionResult res = new TransactionResult();
            try
            {
                var myState = await GetState(context, AccessMode.ReadWrite);
                myState.list.Clear();
            }
            catch (Exception)
            {
                res.exception = true;
            }
            return res;
        }

        public async Task<TransactionResult> Read(MyTransactionContext context, object funcInput)
        {
            TransactionResult res = new TransactionResult();
            try
            {
                var myState = await GetState(context, AccessMode.ReadWrite);
                res.resultObject = myState.list;
            }
            catch (Exception e)
            {
                Console.WriteLine("Read exception: " + e.Message);
                res.exception = true;
            }
            
            return res;
        }

        public async Task<TransactionResult> Append(MyTransactionContext context, object funcInput)
        {
            TransactionResult res = new TransactionResult();
            try
            {
                var myState = await GetState(context, AccessMode.ReadWrite);
                int toAppend = (int)funcInput;
                myState.list.Add(toAppend);
            }
            catch (Exception e)
            {
                Console.WriteLine("Append exception: " + e.Message);
                res.exception = true;
            }
            
            return res;
        }

        public async Task<TransactionResult> Group(MyTransactionContext context, object funcInput)
        {
            TransactionResult res = new TransactionResult();
            List<JepsenOperation> ops = (List<JepsenOperation>)funcInput;

            try
            {
                var myState = await GetState(context, AccessMode.ReadWrite);
                foreach (JepsenOperation op in ops)
                {
                    if (op._opType == JepsenOperation.OpType.Read)
                    {
                        op._ret = new List<int>(myState.list); // deep copy
                    } else if (op._opType == JepsenOperation.OpType.Append)
                    {
                        myState.list.Add(op._val);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Group exception: " + e.Message);
                res.exception = true;
            }

            res.resultObject = ops;

            return res;
        }
    }
}