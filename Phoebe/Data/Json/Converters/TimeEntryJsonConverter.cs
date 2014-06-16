﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Toggl.Phoebe.Data.DataObjects;

namespace Toggl.Phoebe.Data.Json.Converters
{
    public sealed class TimeEntryJsonConverter : BaseJsonConverter
    {
        public async Task<TimeEntryJson> Export (TimeEntryData data)
        {
            var userIdTask = GetRemoteId<UserData> (data.UserId);
            var workspaceIdTask = GetRemoteId<WorkspaceData> (data.WorkspaceId);
            var projectIdTask = GetRemoteId<ProjectData> (data.ProjectId);
            var taskIdTask = GetRemoteId<TaskData> (data.TaskId);
            var tagsTask = GetTimeEntryTags (data.Id);

            return new TimeEntryJson () {
                Id = data.RemoteId,
                ModifiedAt = data.ModifiedAt,
                Description = data.Description,
                IsBillable = data.IsBillable,
                StartTime = data.StartTime,
                StopTime = data.StopTime,
                DurationOnly = data.DurationOnly,
                Duration = EncodeDuration (data),
                Tags = await tagsTask.ConfigureAwait (false),
                UserId = await userIdTask.ConfigureAwait (false),
                WorkspaceId = await workspaceIdTask.ConfigureAwait (false),
                ProjectId = await projectIdTask.ConfigureAwait (false),
                TaskId = await taskIdTask.ConfigureAwait (false),
            };
        }

        private async Task<List<string>> GetTimeEntryTags (Guid id)
        {
            if (id == Guid.Empty)
                return new List<string> (0);
            return await DataStore.GetTimeEntryTagNames (id).ConfigureAwait (false);
        }

        private static long EncodeDuration (TimeEntryData data)
        {
            var now = Time.UtcNow;

            // Calculate time entry duration
            TimeSpan duration;
            if (data.StartTime == DateTime.MinValue) {
                duration = TimeSpan.Zero;
            } else {
                duration = (data.StopTime ?? now) - data.StartTime;
                if (duration < TimeSpan.Zero) {
                    duration = TimeSpan.Zero;
                }
            }

            // Encode the duration
            var encoded = (long)duration.TotalSeconds;
            if (data.State == TimeEntryState.Running) {
                encoded = (long)(encoded - now.ToUnix ().TotalSeconds);
            }
            return encoded;
        }

        private static void DecodeDuration (TimeEntryData data, TimeEntryJson json)
        {
            // Decode duration:
            TimeSpan duration;
            if (json.Duration < 0) {
                data.State = TimeEntryState.Running;
                duration = Time.UtcNow.ToUnix () + TimeSpan.FromSeconds (json.Duration);
            } else {
                data.State = TimeEntryState.Finished;
                duration = TimeSpan.FromSeconds (json.Duration);
            }

            // Set start and stop times based on the duration:
            var now = Time.UtcNow;
            if (data.State == TimeEntryState.Finished) {
                data.StartTime = json.StartTime;
                data.StopTime = json.StartTime + duration;
            } else {
                data.StartTime = now - duration;
                data.StopTime = null;
            }
        }

        private static async Task Merge (TimeEntryData data, TimeEntryJson json)
        {
            var userIdTask = GetLocalId<UserData> (json.UserId);
            var workspaceIdTask = GetLocalId<WorkspaceData> (json.WorkspaceId);
            var projectIdTask = GetLocalId<ProjectData> (json.ProjectId);
            var taskIdTask = GetLocalId<TaskData> (json.TaskId);

            data.Description = json.Description;
            data.IsBillable = json.IsBillable;
            data.DurationOnly = json.DurationOnly;
            data.UserId = await userIdTask.ConfigureAwait (false);
            data.WorkspaceId = await workspaceIdTask.ConfigureAwait (false);
            data.ProjectId = await projectIdTask.ConfigureAwait (false);
            data.TaskId = await taskIdTask.ConfigureAwait (false);
            DecodeDuration (data, json);

            MergeCommon (data, json);
        }

        private static Task ResetTags (TimeEntryData timeEntryData, TimeEntryJson json)
        {
            return DataStore.ExecuteInTransactionAsync (ctx => {
                var con = ctx.Connection;

                // Resolve tags to IDs:
                var tagIds = new List<Guid> ();
                foreach (var tagName in json.Tags) {
                    var id = ctx.GetTagIdFromName (timeEntryData.WorkspaceId, tagName);

                    if (id == null) {
                        // Need to create a new tag:
                        var tagData = new TagData () {
                            Name = tagName,
                            WorkspaceId = timeEntryData.WorkspaceId,
                        };
                        con.Insert (tagData);

                        id = timeEntryData.Id;
                    }

                    tagIds.Add (id.Value);
                }

                // Iterate over TimeEntryTags and determine which to keep and which to discard:
                var inters = con.Table<TimeEntryTagData> ().Where (m => m.TimeEntryId == timeEntryData.Id);
                var toDelete = new List<TimeEntryTagData> ();
                foreach (var inter in inters) {
                    if (tagIds.Contains (inter.TagId)) {
                        tagIds.Remove (inter.TagId);
                    } else {
                        toDelete.Add (inter);
                    }
                }

                // Delete unused tags intermediate rows:
                foreach (var inter in toDelete) {
                    ctx.Delete (inter);
                }

                // Create new intermediate rows:
                foreach (var tagId in tagIds) {
                    ctx.Put (new TimeEntryTagData () {
                        TagId = tagId,
                        TimeEntryId = timeEntryData.Id,
                    });
                }
            });
        }

        public async Task<TimeEntryData> Import (TimeEntryJson json)
        {
            var data = await GetByRemoteId<TimeEntryData> (json.Id.Value).ConfigureAwait (false);

            if (json.DeletedAt.HasValue) {
                if (data != null) {
                    // TODO: Delete TimeEntryTag intermediate data
                    await DataStore.DeleteAsync (data).ConfigureAwait (false);
                    data = null;
                }
            } else if (data == null || data.ModifiedAt < json.ModifiedAt) {
                data = data ?? new TimeEntryData ();
                await Merge (data, json).ConfigureAwait (false);
                data = await DataStore.PutAsync (data).ConfigureAwait (false);
                // Also update tags from the JSON we are merging:
                await ResetTags (data, json).ConfigureAwait (false);
            }

            return data;
        }
    }
}
