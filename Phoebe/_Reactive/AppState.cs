﻿using System;
using System.Linq;
using System.Collections.Generic;
using Toggl.Phoebe._Data;
using Toggl.Phoebe._Data.Models;
using Toggl.Phoebe._Helpers;
using Toggl.Phoebe.Logging;
using XPlatUtils;

namespace Toggl.Phoebe._Reactive
{
    public class AppState
    {
        public SettingsState Settings { get; private set; }
        public Net.AuthResult AuthResult { get; private set; }
        public DownloadResult DownloadResult { get; private set; }
        public FullSyncResult FullSyncResult { get; private set; }

        public UserData User { get; private set; }
        public IReadOnlyDictionary<Guid, IWorkspaceData> Workspaces { get; private set; }
        public IReadOnlyDictionary<Guid, IProjectData> Projects { get; private set; }
        public IReadOnlyDictionary<Guid, IWorkspaceUserData> WorkspaceUsers { get; private set; }
        public IReadOnlyDictionary<Guid, IProjectUserData> ProjectUsers { get; private set; }
        public IReadOnlyDictionary<Guid, IClientData> Clients { get; private set; }
        public IReadOnlyDictionary<Guid, ITaskData> Tasks { get; private set; }
        public IReadOnlyDictionary<Guid, ITagData> Tags { get; private set; }
        public IReadOnlyDictionary<Guid, RichTimeEntry> TimeEntries { get; private set; }

        // AppState instances are immutable snapshots, so it's safe to use a cache for ActiveEntry
        private RichTimeEntry _activeEntryCache = null;
        public RichTimeEntry ActiveEntry
        {
            get {
                if (_activeEntryCache == null) {
                    _activeEntryCache = new RichTimeEntry (new TimeEntryData (), this);
                    if (TimeEntries.Count > 0)
                        _activeEntryCache = TimeEntries.Values.SingleOrDefault (
                                                x => x.Data.State == TimeEntryState.Running) ?? _activeEntryCache;
                }
                return _activeEntryCache;
            }
        }

        AppState (
            SettingsState settings,
            Net.AuthResult authResult,
            DownloadResult downloadResult,
            FullSyncResult fullSyncResult,
            UserData user,
            IReadOnlyDictionary<Guid, IWorkspaceData> workspaces,
            IReadOnlyDictionary<Guid, IProjectData> projects,
            IReadOnlyDictionary<Guid, IWorkspaceUserData> workspaceUsers,
            IReadOnlyDictionary<Guid, IProjectUserData> projectUsers,
            IReadOnlyDictionary<Guid, IClientData> clients,
            IReadOnlyDictionary<Guid, ITaskData> tasks,
            IReadOnlyDictionary<Guid, ITagData> tags,
            IReadOnlyDictionary<Guid, RichTimeEntry> timeEntries)
        {
            Settings = settings;
            AuthResult = authResult;
            DownloadResult = downloadResult;
            FullSyncResult = fullSyncResult;
            User = user;
            Workspaces = workspaces;
            Projects = projects;
            WorkspaceUsers = workspaceUsers;
            ProjectUsers = projectUsers;
            Clients = clients;
            Tasks = tasks;
            Tags = tags;
            TimeEntries = timeEntries;
        }

        public AppState With (
            SettingsState settings = null,
            Net.AuthResult? authResult = null,
            DownloadResult downloadResult = null,
            FullSyncResult fullSyncResult = null,
            UserData user = null,
            IReadOnlyDictionary<Guid, IWorkspaceData> workspaces = null,
            IReadOnlyDictionary<Guid, IProjectData> projects = null,
            IReadOnlyDictionary<Guid, IWorkspaceUserData> workspaceUsers = null,
            IReadOnlyDictionary<Guid, IProjectUserData> projectUsers = null,
            IReadOnlyDictionary<Guid, IClientData> clients = null,
            IReadOnlyDictionary<Guid, ITaskData> tasks = null,
            IReadOnlyDictionary<Guid, ITagData> tags = null,
            IReadOnlyDictionary<Guid, RichTimeEntry> timeEntries = null)
        {
            return new AppState (
                       settings ?? Settings,
                       authResult ?? AuthResult,
                       downloadResult ?? DownloadResult,
                       fullSyncResult ?? FullSyncResult,
                       user ?? User,
                       workspaces ?? Workspaces,
                       projects ?? Projects,
                       workspaceUsers ?? WorkspaceUsers,
                       projectUsers ?? ProjectUsers,
                       clients ?? Clients,
                       tasks ?? Tasks,
                       tags ?? Tags,
                       timeEntries ?? TimeEntries);
        }

        /// <summary>
        /// This doesn't check ModifiedAt or DeletedAt, so call it
        /// always after putting items first in the database
        /// </summary>
        public IReadOnlyDictionary<Guid, T> Update<T> (
            IReadOnlyDictionary<Guid, T> oldItems, IEnumerable<ICommonData> newItems)
        where T : ICommonData
        {
            var dic = oldItems.ToDictionary (x => x.Key, x => x.Value);
            foreach (var newItem in newItems.OfType<T> ()) {
                if (newItem.DeletedAt == null) {
                    if (dic.ContainsKey (newItem.Id)) {
                        dic [newItem.Id] = newItem;
                    } else {
                        dic.Add (newItem.Id, newItem);
                    }
                } else {
                    if (dic.ContainsKey (newItem.Id)) {
                        dic.Remove (newItem.Id);
                    }
                }
            }
            return dic;
        }

        /// <summary>
        /// This doesn't check ModifiedAt or DeletedAt, so call it
        /// always after putting items first in the database
        /// </summary>
        public IReadOnlyDictionary<Guid, RichTimeEntry> UpdateTimeEntries (
            IEnumerable<ICommonData> newItems)
        {
            var dic = TimeEntries.ToDictionary (x => x.Key, x => x.Value);
            foreach (var newItem in newItems.OfType<ITimeEntryData> ()) {
                if (newItem.DeletedAt == null) {
                    if (dic.ContainsKey (newItem.Id)) {
                        dic [newItem.Id] = new RichTimeEntry (
                            newItem, LoadTimeEntryInfo (newItem));
                    } else {
                        dic.Add (newItem.Id, new RichTimeEntry (
                                     newItem, LoadTimeEntryInfo (newItem)));
                    }
                } else {
                    if (dic.ContainsKey (newItem.Id)) {
                        dic.Remove (newItem.Id);
                    }
                }
            }
            return dic;
        }

        public TimeEntryInfo LoadTimeEntryInfo (ITimeEntryData teData)
        {
            var workspaceData = teData.WorkspaceId != Guid.Empty ? Workspaces[teData.WorkspaceId] : new WorkspaceData ();
            var projectData = teData.ProjectId != Guid.Empty ? Projects[teData.ProjectId] : new ProjectData ();
            var clientData = projectData.ClientId != Guid.Empty ? Clients[projectData.ClientId] : new ClientData ();
            var taskData = teData.TaskId != Guid.Empty ? Tasks[teData.TaskId] : new TaskData ();
            var color = (projectData.Id != Guid.Empty) ? projectData.Color : -1;
            var tagsData =
                teData.Tags.Select (
                    x => Tags.Values.SingleOrDefault (y => y.WorkspaceId == teData.WorkspaceId && y.Name == x))
                // TODO: Throw exception if tag was not found?
                .Where (x => x != null)
                .ToList ();

            return new TimeEntryInfo (
                       workspaceData,
                       projectData,
                       clientData,
                       taskData,
                       tagsData,
                       color);
        }

        public IEnumerable<IProjectData> GetUserAccessibleProjects (Guid userId)
        {
            return Projects.Values.Where (
                       p => p.IsActive &&
                       (p.IsPrivate || ProjectUsers.Values.Any (x => x.ProjectId == p.Id && x.UserId == userId)))
                   .OrderBy (p => p.Name);
        }

        public TimeEntryData GetTimeEntryDraft ()
        {
            var userId = User.Id;
            var workspaceId = User.DefaultWorkspaceId;
            var remoteWorkspaceId = User.DefaultWorkspaceRemoteId;
            var durationOnly = User.TrackingMode == TrackingMode.Continue;

            // Create new draft object
            return new TimeEntryData {
                State = TimeEntryState.New,
                UserId = userId,
                WorkspaceId = workspaceId,
                WorkspaceRemoteId = remoteWorkspaceId,
                DurationOnly = durationOnly,
            };
        }

        public static AppState Init ()
        {
            var userData = new UserData ();
            var settings = SettingsState.Init ();
            try {
                if (settings.UserId != Guid.Empty) {
                    var dataStore = ServiceContainer.Resolve<ISyncDataStore> ();
                    userData = dataStore.Table<UserData> ().Single (x => x.Id == settings.UserId);
                }
            } catch (Exception ex) {
                var logger = ServiceContainer.Resolve<ILogger> ();
                logger.Error (typeof (AppState).Name, ex, "UserId in settings not found in db: {0}", ex.Message);
                // When data is corrupt and cannot find user
                settings = settings.With (userId: Guid.Empty);
            }

            return new AppState (
                       settings: settings,
                       authResult: Net.AuthResult.None,
                       downloadResult: DownloadResult.Empty,
                       fullSyncResult:FullSyncResult.Empty,
                       user: userData,
                       workspaces: new Dictionary<Guid, IWorkspaceData> (),
                       projects: new Dictionary<Guid, IProjectData> (),
                       workspaceUsers: new Dictionary<Guid, IWorkspaceUserData> (),
                       projectUsers: new Dictionary<Guid, IProjectUserData> (),
                       clients: new Dictionary<Guid, IClientData> (),
                       tasks: new Dictionary<Guid, ITaskData> (),
                       tags: new Dictionary<Guid, ITagData> (),
                       timeEntries: new Dictionary<Guid, RichTimeEntry> ());
        }
    }

    public class RichTimeEntry
    {
        public TimeEntryInfo Info { get; private set; }
        public ITimeEntryData Data { get; private set; }

        public RichTimeEntry (ITimeEntryData data, TimeEntryInfo info)
        {
            Data = data;
            Info = info;
        }

        public RichTimeEntry (ITimeEntryData data, AppState appState)
        : this (data, appState.LoadTimeEntryInfo (data))
        {
        }

        public override int GetHashCode ()
        {
            return Data.GetHashCode ();
        }

        public override bool Equals (object obj)
        {
            if (ReferenceEquals (this, obj)) {
                return true;
            } else {
                // Quick way to compare time entries
                var other = obj as RichTimeEntry;
                return other != null &&
                       Data.Id == other.Data.Id &&
                       Data.ModifiedAt == other.Data.ModifiedAt &&
                       Data.DeletedAt == other.Data.DeletedAt;
            }
        }
    }

    public class ActiveEntryInfo
    {
        public Guid Id { get; private set; }

        public static ActiveEntryInfo Empty
        {
            get { return new ActiveEntryInfo (Guid.Empty); }
        }

        public ActiveEntryInfo (Guid id)
        {
            Id = id;
        }
    }

    public class FullSyncResult
    {
        public bool IsSyncing { get; private set; }
        public DateTime SyncLastRun { get; private set; }
        public bool HadErrors { get; private set; }

        public static FullSyncResult Empty
        {
            get {
                // Initial Date for full sync.
                // the last 5 days.
                var syncLastRun = Time.UtcNow.AddDays (-5);
                return new FullSyncResult (false, true, syncLastRun);
            }
        }

        public FullSyncResult (bool isSyncing, bool hadErrors, DateTime syncLastRun)
        {
            IsSyncing = isSyncing;
            HadErrors = hadErrors;
            SyncLastRun = syncLastRun;
        }

        public FullSyncResult With (
            bool? isSyncing = null,
            bool? hadErrors = null,
            DateTime? syncLastRun = null)
        {
            return new FullSyncResult (
                       isSyncing.HasValue ? isSyncing.Value : IsSyncing,
                       hadErrors.HasValue ? hadErrors.Value : HadErrors,
                       syncLastRun.HasValue ? syncLastRun.Value : SyncLastRun);
        }
    }

    public class DownloadResult
    {
        public bool IsSyncing { get; private set; }
        public bool HasMore { get; private set; }
        public bool HadErrors { get; private set; }
        public DateTime DownloadFrom { get; private set; }
        public DateTime NextDownloadFrom { get; private set; }

        public static DownloadResult Empty
        {
            get {
                // Set initial pagination Date to the beginning of the next day.
                // So, we can include all entries created -Today-.
                var downloadFrom = Time.UtcNow.Date.AddDays (1);
                return new DownloadResult (false, true, false, downloadFrom, downloadFrom);
            }
        }

        public DownloadResult (
            bool isSyncing, bool hasMore, bool hadErrors,
            DateTime downloadFrom, DateTime nextDownloadFrom)
        {
            IsSyncing = isSyncing;
            HasMore = hasMore;
            HadErrors = hadErrors;
            DownloadFrom = downloadFrom;
            NextDownloadFrom = nextDownloadFrom;
        }

        public DownloadResult With (
            bool? isSyncing = null,
            bool? hasMore = null,
            bool? hadErrors = null,
            DateTime? downloadFrom = null,
            DateTime? nextDownloadFrom = null)
        {
            return new DownloadResult (
                       isSyncing.HasValue ? isSyncing.Value : IsSyncing,
                       hasMore.HasValue ? hasMore.Value : HasMore,
                       hadErrors.HasValue ? hadErrors.Value : HadErrors,
                       downloadFrom.HasValue ? downloadFrom.Value : DownloadFrom,
                       nextDownloadFrom.HasValue ? nextDownloadFrom.Value : NextDownloadFrom);
        }
    }

    public class SettingsState
    {
        // Common Default values
        private static readonly Guid UserIdDefault = Guid.Empty;
        private static readonly DateTime SyncLastRunDefault = DateTime.MinValue;
        private static readonly bool UseDefaultTagDefault = true;
        private static readonly string LastAppVersionDefault = string.Empty;
        private static readonly int LastReportZoomDefault = 0;
        private static readonly bool GroupedEntriesDefault = false;
        private static readonly bool ChooseProjectForNewDefault = false;
        private static readonly int ReportsCurrentItemDefault = 0;
        private static readonly string ProjectSortDefault = "Clients";
        private static readonly string InstallIdDefault = string.Empty;
        // iOS only Default values
        private static readonly string RossPreferredStartViewDefault = string.Empty;
        private static readonly bool RossReadDurOnlyNoticeDefault = false;
        private static readonly DateTime RossIgnoreSyncErrorsUntilDefault = DateTime.MinValue;
        // Android only Default values
        private static readonly string GcmRegistrationIdDefault = string.Empty;
        private static readonly string GcmAppVersionDefault = string.Empty;
        private static readonly bool IdleNotificationDefault = true;
        private static readonly bool ShowNotificationDefault = true;
        private static readonly bool ShowWelcomeDefault = false;

        // Common values
        public Guid UserId {get; private set; }
        public DateTime SyncLastRun { get; private set; }
        public bool UseDefaultTag { get; private set; }
        public string LastAppVersion { get; private set; }
        public int LastReportZoom { get; private set; }
        public bool GroupedEntries { get; private set; }
        public bool ChooseProjectForNew { get; private set; }
        public int ReportsCurrentItem { get; private set; }
        public string ProjectSort { get; private set; }
        public string InstallId  { get; private set; }
        // iOS only  values
        public string RossPreferredStartView { get; private set; }
        public bool RossReadDurOnlyNotice { get; private set; }
        public DateTime RossIgnoreSyncErrorsUntil { get; private set; }
        // Android only  values
        public string GcmRegistrationId { get; private set; }
        public string GcmAppVersion { get; private set; }
        public bool IdleNotification { get; private set; }
        public bool ShowNotification { get; private set; }
        public bool ShowWelcome { get; private set; }

        public static SettingsState Init ()
        {
            // If saved is empty, return default.
            if (Settings.SerializedSettings == string.Empty) {
                var settings = new SettingsState();
                settings.UserId = UserIdDefault;
                settings.SyncLastRun = SyncLastRunDefault;
                settings.UseDefaultTag = UseDefaultTagDefault;
                settings.LastAppVersion = LastAppVersionDefault;
                settings.LastReportZoom = LastReportZoomDefault;
                settings.GroupedEntries = GroupedEntriesDefault;
                settings.ChooseProjectForNew = ChooseProjectForNewDefault;
                settings.ReportsCurrentItem = ReportsCurrentItemDefault;
                settings.ProjectSort = ProjectSortDefault;
                settings.InstallId = InstallIdDefault;
                // iOS only  values
                settings.RossPreferredStartView = RossPreferredStartViewDefault;
                settings.RossReadDurOnlyNotice = RossReadDurOnlyNoticeDefault;
                settings.RossIgnoreSyncErrorsUntil = RossIgnoreSyncErrorsUntilDefault;
                // Android only  values
                settings.GcmRegistrationId = GcmRegistrationIdDefault;
                settings.GcmAppVersion = GcmAppVersionDefault;
                settings.IdleNotification = IdleNotificationDefault;
                settings.ShowNotification = ShowNotificationDefault;
                settings.ShowNotification = ShowWelcomeDefault;
                return settings;
            }
            return Newtonsoft.Json.JsonConvert.DeserializeObject<SettingsState> (Settings.SerializedSettings,
                    Settings.GetNonPublicPropertiesResolverSettings ());
        }

        private T updateNullable<T> (T? value, T @default, Func<T,T> update)
        where T : struct {
            return value.HasValue ? update (value.Value) : @default;
        }

        private T updateReference<T> (T value, T @default, Func<T,T> update)
        where T : class
        {
            return value != null ? update (value) : @default;
        }

        public SettingsState With (
            Guid? userId = null,
            DateTime? syncLastRun = null,
            bool? useTag = null,
            string lastAppVersion = null,
            int? lastReportZoom = null,
            bool? groupedEntries = null,
            bool? chooseProjectForNew = null,
            int? reportsCurrentItem = null,
            string projectSort = null,
            string installId = null,
            // iOS only  values
            string rossPreferredStartView = null,
            bool? rossReadDurOnlyNotice = null,
            DateTime? rossIgnoreSyncErrorsUntil = null,
            // Android only  values
            string gcmRegistrationId = null,
            string gcmAppVersion = null,
            bool? idleNotification = null,
            bool? showNotification = null,
            bool? showWelcome = null)
        {
            var copy = Init();
            updateNullable (userId, copy.UserId, x => copy.UserId = x);
            updateNullable (syncLastRun, copy.SyncLastRun, x => copy.SyncLastRun = x);
            updateNullable (useTag, copy.UseDefaultTag, x => copy.UseDefaultTag = x);
            updateReference (lastAppVersion, copy.LastAppVersion, x => copy.LastAppVersion = x);
            updateNullable (lastReportZoom, copy.LastReportZoom, x => copy.LastReportZoom = x);
            updateNullable (groupedEntries, copy.GroupedEntries, x => copy.GroupedEntries = x);
            updateNullable (chooseProjectForNew, copy.ChooseProjectForNew, x => copy.ChooseProjectForNew = x);
            updateNullable (reportsCurrentItem, copy.ReportsCurrentItem, x => copy.ReportsCurrentItem = x);
            updateReference (projectSort, copy.ProjectSort, x => copy.ProjectSort = x);
            updateReference (installId, copy.InstallId, x => copy.InstallId = x);
            // iOS only  values
            updateReference (rossPreferredStartView, copy.RossPreferredStartView, x => copy.RossPreferredStartView = x);
            updateNullable (rossReadDurOnlyNotice, copy.RossReadDurOnlyNotice, x => copy.RossReadDurOnlyNotice = x);
            updateNullable (rossIgnoreSyncErrorsUntil, copy.RossIgnoreSyncErrorsUntil, x => copy.RossIgnoreSyncErrorsUntil = x);
            // Android only  values
            updateReference (gcmRegistrationId, copy.GcmRegistrationId, x => copy.GcmRegistrationId = x);
            updateReference (gcmAppVersion, copy.GcmAppVersion, x => copy.GcmAppVersion = x);
            updateNullable (idleNotification, copy.IdleNotification, x => copy.IdleNotification = x);
            updateNullable (showNotification, copy.ShowNotification, x => copy.ShowNotification = x);
            updateNullable (showWelcome, copy.ShowWelcome, x => copy.ShowWelcome = x);

            // Save new copy serialized
            Settings.SerializedSettings = Newtonsoft.Json.JsonConvert.SerializeObject (copy);
            return copy;
        }
    }
}

