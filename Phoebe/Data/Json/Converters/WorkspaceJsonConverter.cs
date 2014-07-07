﻿using System;
using System.Threading.Tasks;
using Toggl.Phoebe.Data.DataObjects;

namespace Toggl.Phoebe.Data.Json.Converters
{
    public sealed class WorkspaceJsonConverter : BaseJsonConverter
    {
        public Task<WorkspaceJson> Export (WorkspaceData data)
        {
            return Task.FromResult (new WorkspaceJson () {
                Id = data.RemoteId,
                ModifiedAt = data.ModifiedAt.ToUtc (),
                Name = data.Name,
                IsPremium = data.IsPremium,
                DefaultRate = data.DefaultRate,
                DefaultCurrency = data.DefaultCurrency,
                OnlyAdminsMayCreateProjects = data.ProjectCreationPrivileges == AccessLevel.Admin,
                OnlyAdminsSeeBillableRates = data.BillableRatesVisibility == AccessLevel.Admin,
                RoundingMode = data.RoundingMode,
                RoundingPercision = data.RoundingPercision,
                LogoUrl = data.LogoUrl,
            });
        }

        private static void Merge (WorkspaceData data, WorkspaceJson json)
        {
            data.Name = json.Name;
            data.IsPremium = json.IsPremium;
            data.DefaultRate = json.DefaultRate;
            data.DefaultCurrency = json.DefaultCurrency;
            data.ProjectCreationPrivileges = json.OnlyAdminsMayCreateProjects ? AccessLevel.Admin : AccessLevel.Regular;
            data.BillableRatesVisibility = json.OnlyAdminsSeeBillableRates ? AccessLevel.Admin : AccessLevel.Regular;
            data.RoundingMode = json.RoundingMode;
            data.RoundingPercision = json.RoundingPercision;
            data.LogoUrl = json.LogoUrl;

            MergeCommon (data, json);
        }

        public async Task<WorkspaceData> Import (WorkspaceJson json, Guid? localIdHint = null, bool forceUpdate = false)
        {
            var data = await GetByRemoteId<WorkspaceData> (json.Id.Value, localIdHint).ConfigureAwait (false);

            if (json.DeletedAt.HasValue) {
                if (data != null) {
                    await DataStore.DeleteAsync (data).ConfigureAwait (false);
                    data = null;
                }
            } else if (data == null || forceUpdate || data.ModifiedAt < json.ModifiedAt) {
                data = data ?? new WorkspaceData ();
                Merge (data, json);
                data = await DataStore.PutAsync (data).ConfigureAwait (false);
            }

            return data;
        }
    }
}
