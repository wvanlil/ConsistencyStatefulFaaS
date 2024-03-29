﻿using System.Threading.Tasks;

namespace Persist.Interfaces
{
    public interface IPersistSingletonGroup
    {
        void Init(int numCPUPerSilo);
        IPersistWorker GetSingleton(int index);
        long GetIOCount();
        void SetIOCount();
    }

    public interface IPersistWorker
    {
        Task Write(byte[] value);
        long GetIOCount();
        void SetIOCount();

        void CleanFile();
    }
}
