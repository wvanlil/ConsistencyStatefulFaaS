﻿using System;
using Utilities;
using TPCC.Interfaces;
using Persist.Interfaces;
using System.Threading.Tasks;
using Concurrency.Implementation;
using Orleans.Transactions;

namespace TPCC.Grains
{
    [Serializable]
    public class WarehouseInfo : ICloneable
    {
        public Warehouse warehouse;

        public WarehouseInfo()
        {
        }

        public WarehouseInfo(WarehouseInfo warehouse_info)
        {
            warehouse = warehouse_info.warehouse;
        }

        object ICloneable.Clone()
        {
            return new WarehouseInfo(this);
        }
    }

    public class WarehouseGrain : TransactionExecutionGrain<WarehouseInfo>, IWarehouseGrain
    {
        public WarehouseGrain(IPersistSingletonGroup persistSingletonGroup) : base(persistSingletonGroup, "TPCC.Grains.WarehouseGrain")
        {
        }

        // input: int     W_ID
        // output: null
        public async Task<TransactionResult> Init(MyTransactionContext context, object funcInput)
        {
            var res = new TransactionResult();
            try
            {
                var W_ID = (int)funcInput;   // W_ID
                var myState = await GetState(context, AccessMode.ReadWrite);
                myState.warehouse = InMemoryDataGenerator.GenerateWarehouseInfo(W_ID);
            }
            catch (Exception)
            {
                res.exception = true;
            }
            return res;
        }

        // input: null
        // output: float    D_TAX
        public async Task<TransactionResult> GetWTax(MyTransactionContext context, object funcInput)
        {
            var res = new TransactionResult();
            try
            {
                var myState = await GetState(context, AccessMode.Read);
                res.resultObject = myState.warehouse.W_TAX;
            }
            catch (Exception)
            {
                res.exception = true;
            }
            return res;
        }
    }
}