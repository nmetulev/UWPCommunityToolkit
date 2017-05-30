using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Microsoft.Toolkit.Uwp.Helpers
{
    public class MultiInstanceHelper
    {
        Action _initializeMainPage = null;
        List<ViewLifetimeControl> _secondaryViews = new List<ViewLifetimeControl>();

        /// <summary>
        /// Private singleton field.
        /// </summary>
        private static MultiInstanceHelper instance;

        /// <summary>
        /// Gets public singleton property.
        /// </summary>
        public static MultiInstanceHelper Instance => instance ?? (instance = new MultiInstanceHelper());

        public async Task Register(Action initializeMainPage)
        {
            _initializeMainPage = initializeMainPage;
            var selectedView = await createMainPageAsync();
            if (null != selectedView)
            {
                selectedView.StartViewInUse();
                await ApplicationViewSwitcher.TryShowAsStandaloneAsync(selectedView.Id, ViewSizePreference.Default);

                await selectedView.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    Window.Current.Activate();
                });

                selectedView.StopViewInUse();
            }
        }

        private async Task<ViewLifetimeControl> createMainPageAsync()
        {
            ViewLifetimeControl viewControl = null;
            await CoreApplication.CreateNewView().Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                // This object is used to keep track of the views and important
                // details about the contents of those views across threads
                // In your app, you would probably want to track information
                // like the open document or page inside that window
                viewControl = ViewLifetimeControl.CreateForCurrentView();

                // Increment the ref count because we just created the view and we have a reference to it
                viewControl.StartViewInUse();

                _initializeMainPage();
                // This is a change from 8.1: In order for the view to be displayed later it needs to be activated.
                Window.Current.Activate();
            });

            _secondaryViews.Add(viewControl);

            return viewControl;
        }
    }
}
