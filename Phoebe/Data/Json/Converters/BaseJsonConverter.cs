﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Toggl.Phoebe.Data.DataObjects;
using XPlatUtils;

namespace Toggl.Phoebe.Data.Json.Converters
{
    public abstract class BaseJsonConverter
    {
        protected static void MergeCommon (CommonData data, CommonJson json)
        {
            data.RemoteId = json.Id;
            data.RemoteRejected = false;
            data.DeletedAt = null;
            data.ModifiedAt = json.ModifiedAt.ToUtc ();
            data.IsDirty = false;
        }

        protected static async Task<T> GetByRemoteId<T> (long remoteId, Guid? localIdHint)
            where T : CommonData, new()
        {
            var query = DataStore.Table<T> ();
            if (localIdHint != null) {
                var localId = localIdHint.Value;
                query = query.Where (r => r.Id == localId || r.RemoteId == remoteId);
            } else {
                query = query.Where (r => r.RemoteId == remoteId);
            }

            var res = await query.QueryAsync ().ConfigureAwait (false);
            return res.FirstOrDefault (data => data.RemoteId == remoteId) ?? res.FirstOrDefault ();
        }

        protected static async Task<long> GetRemoteId<T> (Guid id)
            where T : CommonData
        {
            var remoteId = await DataStore.GetRemoteId<T> (id).ConfigureAwait (false);
            if (remoteId == 0) {
                throw new InvalidOperationException (String.Format (
                    "Cannot export data with local-only relation ({0}#{1}) to JSON.",
                    typeof(T).Name, id
                ));
            }
            return remoteId;
        }

        protected static async Task<long?> GetRemoteId<T> (Guid? id)
            where T : CommonData
        {
            if (id == null)
                return null;
            var remoteId = await DataStore.GetRemoteId<T> (id.Value).ConfigureAwait (false);
            if (remoteId == 0) {
                throw new InvalidOperationException (String.Format (
                    "Cannot export data with local-only relation ({0}#{1}) to JSON.",
                    typeof(T).Name, id
                ));
            }
            return remoteId;
        }

        protected static async Task<Guid> GetLocalId<T> (long remoteId)
            where T : CommonData, new()
        {
            if (remoteId == 0) {
                throw new ArgumentException ("Remote Id cannot be zero.", "remoteId");
            }
            var id = await DataStore.GetLocalId<T> (remoteId).ConfigureAwait (false);
            if (id == Guid.Empty)
                id = await CreatePlaceholder<T> (remoteId);
            return id;
        }

        protected static async Task<Guid?> GetLocalId<T> (long? remoteId)
            where T : CommonData, new()
        {
            if (remoteId == null)
                return null;
            var id = await DataStore.GetLocalId<T> (remoteId.Value).ConfigureAwait (false);
            if (id == Guid.Empty)
                id = await CreatePlaceholder<T> (remoteId.Value);
            return id;
        }

        private static async Task<Guid> CreatePlaceholder<T> (long remoteId)
            where T : CommonData, new()
        {
            var data = await DataStore.PutAsync (new T () {
                RemoteId = remoteId,
                ModifiedAt = DateTime.MinValue,
            });
            return data.Id;
        }

        protected static IDataStore DataStore {
            get { return ServiceContainer.Resolve<IDataStore> (); }
        }
    }
}
