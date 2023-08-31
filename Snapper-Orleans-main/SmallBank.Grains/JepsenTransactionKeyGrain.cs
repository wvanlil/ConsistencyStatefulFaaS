using Orleans;
using Custom;
using Orleans.Concurrency;
using System.Text.RegularExpressions;
using SmallBank.Interfaces;
using System.Collections.Generic;
using System.Threading.Tasks;
using Utilities;
using Concurrency.Implementation;
using System;
using Persist.Interfaces;

namespace SmallBank.Grains
{
    [Serializable]
    public class Nonce2 : ICloneable
    {
        public int nonce;

        public Nonce2()
        {
        }
        object ICloneable.Clone()
        {
            return new Nonce2();
        }
    }

    public class JepsenTransactionKeyGrain : TransactionExecutionGrain<Nonce>, IJepsenTransactionKeyGrain
    {

        public JepsenTransactionKeyGrain(IPersistSingletonGroup persistSingletonGroup) : base(persistSingletonGroup, "SmallBank.Grains.JepsenTransactionGrain")
        {
        }

        public async Task<List<JepsenOperation>> Execute(List<JepsenOperation> operations, MyTransactionContext context)
        {
            foreach (JepsenOperation operation in operations)
            {
                IDataGrain current = GrainFactory.GetGrain<IDataGrain>(operation._target);
                if (operation._opType == JepsenOperation.OpType.Read)
                {
                    var funcCall = new FunctionCall("Read", null, typeof(DataGrain));
                    var int_res = await CallGrain(context, operation._target, "SmallBank.Grains.DataGrain", funcCall);
                    operation._ret = (List<int>)int_res.resultObject;
                }
                else if (operation._opType == JepsenOperation.OpType.Append)
                {
                    var funcCall = new FunctionCall("Append", operation._val, typeof(DataGrain));
                    var int_res = await CallGrain(context, operation._target, "SmallBank.Grains.DataGrain", funcCall);
                    // Could also read here after the write, Jepsen might like this
                }
                else if (operation._opType == JepsenOperation.OpType.Wait)
                {
                    await Task.Delay(operation._val);
                }
            }

            return operations;
        }

        public async Task<List<JepsenOperation>> ExecuteGroups(List<JepsenOperation> operations, MyTransactionContext context)
        {
            Dictionary<int, List<JepsenOperation>> buckets = new Dictionary<int, List<JepsenOperation>>();

            int index = 0;
            foreach (JepsenOperation operation in operations)
            {
                operation._index = index;
                if (buckets.ContainsKey(operation._target)) {
                    buckets[operation._target].Add(operation);
                } else
                {
                    buckets.Add(operation._target, new List<JepsenOperation>() { operation });
                }
                index++;
            }

            foreach (KeyValuePair<int, List<JepsenOperation>> bucket in buckets)
            {
                var funcCall = new FunctionCall("Group", bucket.Value, typeof(DataGrain));
                var bucket_res = await CallGrain(context, bucket.Key, "SmallBank.Grains.DataGrain", funcCall);
                var list_res = (List<JepsenOperation>)bucket_res.resultObject;
                foreach (var operation in list_res)
                {
                    operations[operation._index] = operation;
                }
            }

            return operations;
        }

        public async Task<TransactionResult> ParseAndExecute(MyTransactionContext context, object funcInput)
        {
            var myState = await GetState(context, AccessMode.ReadWrite); // Access state is necessary?!
            TransactionResult res = new TransactionResult();
            // \[[^\[\]]+?\]
            // https://regexr.com/
            // [[:r 8 nil] [:r 9 nil]]
            Regex regex = new Regex("\\[[^\\[\\]]+?\\]");

            string opstring = (string)funcInput;

            Console.WriteLine("Opstring: " + opstring);

            MatchCollection matches = regex.Matches(opstring);

            var matchlist = new List<string>();
            foreach (Match match in matches)
            {
                matchlist.Add(match.Value);
            }
            //var matchlist = matches.Cast<Match>().Select(match => match.Value).ToList();

            List<JepsenOperation> transaction = new List<JepsenOperation>();

            foreach (string currentop in matchlist)
            {
                string[] parts = currentop.Substring(1, currentop.Length - 2).Split(' ');

                JepsenOperation.OpType opType;
                if (parts[0].Equals(":r"))
                {
                    opType = JepsenOperation.OpType.Read;
                }
                else // if (parts[0].Equals(":append"))
                {
                    opType = JepsenOperation.OpType.Append;
                }

                if (parts[2].Equals("nil"))
                {
                    parts[2] = "0";
                }

                JepsenOperation jop = new JepsenOperation(opType, int.Parse(parts[1]), int.Parse(parts[2]));
                transaction.Add(jop);
            }

            List<JepsenOperation> list;
            list = await Execute(transaction, context);


            res.resultObject = FormString(list);

            Console.WriteLine("Just before returning from initial call");
            Console.WriteLine(res.resultObject);

            return res;

        }

        public async Task<TransactionResult> ParseAndExecuteGroup(MyTransactionContext context, object funcInput)
        {
            var myState = await GetState(context, AccessMode.ReadWrite); // Access state is necessary?!
            TransactionResult res = new TransactionResult();
            // \[[^\[\]]+?\]
            // https://regexr.com/
            // [[:r 8 nil] [:r 9 nil]]
            Regex regex = new Regex("\\[[^\\[\\]]+?\\]");

            string opstring = (string)funcInput;

            Console.WriteLine("Opstring: " + opstring);

            MatchCollection matches = regex.Matches(opstring);

            var matchlist = new List<string>();
            foreach (Match match in matches)
            {
                matchlist.Add(match.Value);
            }
            //var matchlist = matches.Cast<Match>().Select(match => match.Value).ToList();

            List<JepsenOperation> transaction = new List<JepsenOperation>();

            foreach (string currentop in matchlist)
            {
                string[] parts = currentop.Substring(1, currentop.Length - 2).Split(' ');

                JepsenOperation.OpType opType;
                if (parts[0].Equals(":r"))
                {
                    opType = JepsenOperation.OpType.Read;
                }
                else // if (parts[0].Equals(":append"))
                {
                    opType = JepsenOperation.OpType.Append;
                }

                if (parts[2].Equals("nil"))
                {
                    parts[2] = "0";
                }

                JepsenOperation jop = new JepsenOperation(opType, int.Parse(parts[1]), int.Parse(parts[2]));
                transaction.Add(jop);
            }

            List<JepsenOperation> list;
            list = await ExecuteGroups(transaction, context);


            res.resultObject = FormString(list);

            Console.WriteLine("Just before returning from initial call");
            Console.WriteLine(res.resultObject);

            return res;

        }

        public string FormString(List<JepsenOperation> list)
        {
            string ret = "[";

            foreach (JepsenOperation op in list)
            {
                ret += "[";
                if (op._opType == JepsenOperation.OpType.Read)
                {
                    ret += ":r ";
                    ret += op._target.ToString();
                    ret += " [";
                    foreach (int i in op._ret)
                    {
                        ret += i.ToString();
                        ret += " ";
                    }
                    if (op._ret.Count > 0)
                    {
                        ret = ret.Substring(0, ret.Length - 1);
                    }
                    ret += "]] ";
                }
                else // if (op._opType == JepsenOperation.OpType.Append)
                {
                    ret += ":append ";
                    ret += op._target.ToString() + " " + op._val.ToString() + "] ";
                }
            }
            if (list.Count > 0)
            {
                ret = ret.Substring(0, ret.Length - 1);
            }
            ret += "]";

            return ret;
        }
    }
}
