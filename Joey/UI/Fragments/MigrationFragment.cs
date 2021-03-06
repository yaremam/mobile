using System;
using System.Threading.Tasks;
using Android.OS;
using Android.Support.V7.App;
using Android.Views;
using Android.Widget;
using Toggl.Joey.UI.Activities;
using Toggl.Phoebe;
using Toggl.Phoebe.Data;
using Toggl.Phoebe.Reactive;
using XPlatUtils;
using Fragment = Android.Support.V4.App.Fragment;

namespace Toggl.Joey.UI.Fragments
{
    public class MigrationFragment : Fragment
    {
        public const string OLD_VERSION_DB = "old_version";

        private bool userTriedAgain;
        private TextView topLabel;
        private TextView descLabel;
        private TextView discardLabel;
        private TextView discardDesc;
        private TextView percente;
        private ProgressBar progressBar;
        private Button tryAgainBtn;
        private Button discardBtn;
        private Button startBtn;
        private ImageView toggler1;
        private ImageView toggler2;

        private Handler handler = new Handler();

        public MigrationFragment()
        {
        }

        public MigrationFragment(IntPtr jref, Android.Runtime.JniHandleOwnership xfer) : base(jref, xfer)
        {
        }

        public static MigrationFragment NewInstance(int oldVersion)
        {
            // TODO Block press back button from
            // this screen until migration is completed??
            var fragment = new MigrationFragment();

            Bundle args = new Bundle();
            args.PutInt(OLD_VERSION_DB, oldVersion);
            fragment.Arguments = args;

            return fragment;
        }

        public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate(Resource.Layout.MigrationFragment, container, false);

            topLabel = view.FindViewById<TextView>(Resource.Id.topLabel);
            descLabel = view.FindViewById<TextView>(Resource.Id.descLabel);
            discardLabel = view.FindViewById<TextView>(Resource.Id.discardLabel);
            discardDesc = view.FindViewById<TextView>(Resource.Id.discardDesc);
            percente = view.FindViewById<TextView>(Resource.Id.percente);
            progressBar = view.FindViewById<ProgressBar>(Resource.Id.migrationProgressBar);
            tryAgainBtn = view.FindViewById<Button>(Resource.Id.tryAgainBtn);
            discardBtn = view.FindViewById<Button>(Resource.Id.discardBtn);
            toggler1 = view.FindViewById<ImageView>(Resource.Id.toggler1);
            toggler2 = view.FindViewById<ImageView>(Resource.Id.toggler2);
            startBtn = view.FindViewById<Button>(Resource.Id.migrationStartBtn);

            return view;
        }

        public override void OnViewCreated(View view, Bundle savedInstanceState)
        {
            base.OnViewCreated(view, savedInstanceState);
            setProgress(0);
            MigrateDatabase();
            tryAgainBtn.Click += (sender, e) =>
            {
                MigrateDatabase();
                userTriedAgain = true;
            };
            discardBtn.Click += (sender, e) =>
            {
                // Show confirmation dialog!
                var dialog = new AlertDialog.Builder(Activity)
                .SetTitle(Resource.String.MigratingDiscardDialogTitle)
                .SetMessage(Resource.String.MigratingDiscardDialogMsg)
                .SetPositiveButton(Resource.String.MigratingDiscardConfirm, delegate
                {
                    // Reset DBs and state.
                    // Set initial dummy data.
                    DatabaseHelper.ResetToDBVersion(SyncSqliteDataStore.DB_VERSION);
                    RxChain.Send(new DataMsg.ResetState());
                    RxChain.Send(new DataMsg.NoUserDataPut());
                })
                .SetNegativeButton(Resource.String.MigratingDiscardCancel, delegate { })
                .Create();
                dialog.Show();
            };
            startBtn.Click += (sender, e) =>
            {
                // Reset fragments using correct info.
                // Corner case related with not logged users:
                // ApiToken doesn't change and we have to trigger the
                // framework navigation manually.
                ((MainDrawerActivity)Activity).ResetFragmentNavigation(StoreManager.Singleton.AppState.User);
            };
        }

        private void MigrateDatabase()
        {
            Task.Run(() =>
            {
                var oldVersion = Arguments.GetInt(OLD_VERSION_DB);
                var migrationResult = DatabaseHelper.Migrate(
                                          ServiceContainer.Resolve<IPlatformUtils>().SQLiteInfo,
                                          DatabaseHelper.GetDatabaseDirectory(),
                                          oldVersion, SyncSqliteDataStore.DB_VERSION,
                                          setProgress
                                      );
                if (migrationResult)
                {
                    Activity.RunOnUiThread(() => DisplaySuccessState());
                    RxChain.Send(new DataMsg.InitStateAfterMigration());
                }
                else
                {
                    Activity.RunOnUiThread(() =>
                    {
                        if (!userTriedAgain)
                            DisplayErrorState();
                        else
                            DisplayDiscardState();
                    });
                }
            });

            DisplayInitialState();
        }

        public override void OnDestroyView()
        {
            handler.RemoveCallbacksAndMessages(null);
            base.OnDestroyView();
        }

        private void DisplayInitialState()
        {
            topLabel.Text = Resources.GetString(Resource.String.MigratingUpdateTitle);
            descLabel.Text = Resources.GetString(Resource.String.MigratingUpdateDesc);
            progressBar.Visibility = ViewStates.Visible;
            toggler1.Visibility = ViewStates.Visible;
            percente.Visibility = ViewStates.Visible;

            toggler2.Visibility = ViewStates.Gone;
            tryAgainBtn.Visibility = ViewStates.Gone;
            discardBtn.Visibility = ViewStates.Gone;
            discardDesc.Visibility = ViewStates.Gone;
        }

        private void DisplaySuccessState()
        {
            topLabel.Text = Resources.GetString(Resource.String.MigratingSuccessTitle); ;
            descLabel.Text = Resources.GetString(Resource.String.MigratingSuccessDesc);
            toggler2.Visibility = ViewStates.Visible;

            toggler1.Visibility = ViewStates.Gone;
            progressBar.Visibility = ViewStates.Gone;
            tryAgainBtn.Visibility = ViewStates.Gone;
            percente.Visibility = ViewStates.Gone;

            // Start btn is shown after 3 seconds
            // if Success state is still visible.
            handler.PostDelayed(() =>
            {
                startBtn.Visibility = ViewStates.Visible;
            }, 3000);
        }

        private void DisplayErrorState()
        {
            topLabel.Text = Resources.GetString(Resource.String.MigratingTryTitle);
            descLabel.Text = Resources.GetString(Resource.String.MigratingTryDesc);
            tryAgainBtn.Visibility = ViewStates.Visible;

            toggler1.Visibility = ViewStates.Gone;
            toggler2.Visibility = ViewStates.Gone;
            percente.Visibility = ViewStates.Gone;
            progressBar.Visibility = ViewStates.Gone;
            discardBtn.Visibility = ViewStates.Gone;
            discardDesc.Visibility = ViewStates.Gone;
        }

        private void DisplayDiscardState()
        {
            topLabel.Text = Resources.GetString(Resource.String.MigratingFeedbackTitle);
            descLabel.Text = Resources.GetString(Resource.String.MigratingFeedbackDesc);
            discardLabel.Visibility = ViewStates.Visible;
            discardDesc.Visibility = ViewStates.Visible;
            discardBtn.Visibility = ViewStates.Visible;

            toggler1.Visibility = ViewStates.Gone;
            toggler2.Visibility = ViewStates.Gone;
            percente.Visibility = ViewStates.Gone;
            progressBar.Visibility = ViewStates.Gone;
            tryAgainBtn.Visibility = ViewStates.Gone;
        }

        private void setProgress(float percentage)
        {
            Activity.RunOnUiThread(() =>
            {
                var per = Math.Truncate(percentage * 100);
                progressBar.Progress = Convert.ToInt16(per);
                percente.Text = per + " %";
            });
        }
    }
}
