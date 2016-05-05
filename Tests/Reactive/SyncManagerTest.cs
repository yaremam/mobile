﻿using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Toggl.Phoebe.Data;
using Toggl.Phoebe.Data.Json;
using Toggl.Phoebe.Data.Models;
using Toggl.Phoebe.Net;
using Toggl.Phoebe.Reactive;
using XPlatUtils;

namespace Toggl.Phoebe.Tests.Reactive
{
    [TestFixture]
    public class SyncManagerTest : Test
    {
        NetworkSwitcher networkSwitcher;
        readonly ToggleClientMock togglClient = new ToggleClientMock();

        public override void Init()
        {
            base.Init();
            var platformUtils = new PlatformUtils();
            ServiceContainer.RegisterScoped<IPlatformUtils> (platformUtils);
            ServiceContainer.RegisterScoped<ITogglClient> (togglClient);
            networkSwitcher = new NetworkSwitcher();
            ServiceContainer.RegisterScoped<INetworkPresence> (networkSwitcher);
            RxChain.Init(Util.GetInitAppState().With(user: new UserData() { ApiToken = "Dummy Token" }));
        }

        public override void Cleanup()
        {
            base.Cleanup();
            RxChain.Cleanup();
        }

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            togglClient.ReceivedItems.Clear();
        }

        [Test]
        public async Task TestSendMessageWithoutConnection()
        {
            var tcs = Util.CreateTask<bool> ();
            var te = Util.CreateTimeEntryData(DateTime.Now);
            networkSwitcher.SetNetworkConnection(false);

            RxChain.Send(new DataMsg.TimeEntryPut(te), new RxChain.Continuation((_, sent, queued) =>
            {
                try
                {
                    // As there's no connection, message should have been enqueued
                    Assert.That(queued.Any(x => x.Data.Id == te.Id), Is.True);
                    Assert.That(0, Is.EqualTo(sent.Count()));
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            }));

            await tcs.Task;
        }

        [Test]
        public async Task TestSendMessageWithConnection()
        {
            var tcs = Util.CreateTask<bool> ();
            var te = Util.CreateTimeEntryData(DateTime.Now);
            networkSwitcher.SetNetworkConnection(true);

            RxChain.Send(
                new DataMsg.TimeEntryPut(te), new RxChain.Continuation((_, sent, queued) =>
            {
                try
                {
                    // As there's connection, message should have been sent
                    Assert.That(queued.Any(x => x.Data.Id == te.Id), Is.False);
                    Assert.That(1, Is.EqualTo(sent.Count()));
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            }));

            await tcs.Task;
        }

        [Test]
        public async Task TestTrySendMessageAndReconnect()
        {
            var tcs = Util.CreateTask<bool> ();
            var te = Util.CreateTimeEntryData(DateTime.Now);
            var te2 = Util.CreateTimeEntryData(DateTime.Now + TimeSpan.FromMinutes(5));

            networkSwitcher.SetNetworkConnection(false);
            RxChain.Send(new DataMsg.TimeEntryPut(te), new RxChain.Continuation((_, sent, queued) =>
            {
                try
                {
                    // As there's no connection, message should have been enqueued
                    Assert.That(queued.Any(x => x.Data.Id == te.Id), Is.True);
                    Assert.That(0, Is.EqualTo(sent.Count()));
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            }));

            networkSwitcher.SetNetworkConnection(true);
            RxChain.Send(new DataMsg.TimeEntryPut(te2), new RxChain.Continuation((_, sent, queued) =>
            {
                try
                {
                    // As there's connection, messages should have been sent
                    Assert.That(queued.Any(x => x.Data.Id == te.Id || x.Data.Id == te2.Id), Is.False);
                    Assert.That(sent.Count() > 0, Is.True);
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            }));

            await tcs.Task;
        }

        [Test]
        public async Task TestCreateEntryOfflineDeleteAndReconnect()
        {
            var tcs = Util.CreateTask<bool>();
            var te = Util.CreateTimeEntryData(DateTime.Now);

            networkSwitcher.SetNetworkConnection(false);

            RxChain.Send(new DataMsg.TimeEntryPut(te));

            RxChain.Send(new DataMsg.TimeEntriesRemove(te));

            networkSwitcher.SetNetworkConnection(true);
            RxChain.Send(new ServerRequest.GetChanges(), new RxChain.Continuation((_, sent, queued) =>
            {
                try
                {
                    // As there's connection, messages should have been sent
                    Assert.That(queued.Count(), Is.EqualTo(0));
                    // As the entry is deleted, it shouldn't be included in the sent items
                    // (this is to prevent it reappears again in the AppState after deletion)
                    Assert.That(sent.Count(), Is.EqualTo(0));
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            }));

            await tcs.Task;
        }

        [Test]
        public async Task TestCreateNewCommonData()
        {
            // Set network as connected.
            networkSwitcher.SetNetworkConnection(true);
            var mapper = new JsonMapper();
            var tcs = Util.CreateTask<bool> ();
            var te = Util.CreateTimeEntryData(DateTime.Now);

            networkSwitcher.SetNetworkConnection(true);
            RxChain.Send(new DataMsg.TimeEntryPut(te), new RxChain.Continuation((_, sent, queued) =>
            {
                try
                {
                    var commonData = togglClient.ReceivedItems.FirstOrDefault();
                    var remoteTe = mapper.Map<TimeEntryData> (commonData);

                    Assert.That(remoteTe.Description, Is.EqualTo(te.Description));
                    Assert.That(remoteTe.RemoteId, Is.Not.Null);

                    var dataStore = ServiceContainer.Resolve<ISyncDataStore> ();
                    // Check item has been correctly saved in database
                    Assert.That(dataStore.Table<TimeEntryData> ()
                                .SingleOrDefault(x => x.Id == te.Id), Is.Not.Null);
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            }));
            await tcs.Task;
        }
    }
}
