﻿using System;
using System.Collections.Generic;
using SQLite.Net;
using Toggl.Phoebe._Data.Models;

namespace Toggl.Phoebe._Data
{
    public interface ISyncDataStore
    {
        TableQuery<T> Table<T> () where T : CommonData, new();
        IReadOnlyList<ICommonData> Update (Action<ISyncDataStoreContext> worker);

        int GetSize (string queueId);
        bool TryEnqueue (string queueId, string json);
        bool TryDequeue (string queueId, out string json);
        bool TryPeek (string queueId, out string json);
    }

    public interface ISyncDataStoreContext
    {
        void Put (ICommonData obj);
        void Delete (ICommonData obj);
        IReadOnlyList<ICommonData> UpdatedItems { get; }
    }
}
