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
    public class Nonce : ICloneable
    {
        public int nonce;

        public Nonce()
        {
        }
        object ICloneable.Clone()
        {
            return new Nonce();
        }
    }

    [StatelessWorker]
    public class JepsenTransactionGrain : TransactionExecutionGrain<Nonce>, IJepsenTransactionGrain
    {

        public JepsenTransactionGrain(IPersistSingletonGroup persistSingletonGroup) : base(persistSingletonGroup, "SmallBank.Grains.JepsenTransactionGrain")
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

        public async Task<TransactionResult> ParseAndExecute(MyTransactionContext context, object funcInput)
        {
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

                Console.WriteLine(parts[0]);
                Console.WriteLine(parts[1]);
                Console.WriteLine(parts[2]);

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

            List<JepsenOperation> list = await Execute(transaction, context);

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
