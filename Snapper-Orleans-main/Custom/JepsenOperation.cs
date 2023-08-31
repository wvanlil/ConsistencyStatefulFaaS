using System.Collections.Generic;

namespace Custom
{
    public class JepsenOperation
    {
        public enum OpType
        {
            Read,
            Append,
            Wait
        }

        public OpType _opType;
        public int _target;
        public int _val;
        public List<int>? _ret;

        public int _index;

        public JepsenOperation(OpType opType, int target, int val)
        {
            this._opType = opType;
            this._target = target;
            this._val = val;
        }

        public JepsenOperation(OpType opType, int target, int val, int index)
        {
            this._opType = opType;
            this._target = target;
            this._val = val;
            this._index = index;
        }

        public override string ToString()
        {
            string temp = "";
            if (_ret != null)
            {
                foreach (var item in _ret)
                {
                    temp += item.ToString() + ", ";
                }
            }
            return $"{_opType}: {_val}, Target: {_target}, result: {temp}";
        }
    }
}