using System;
using System.Threading;
using Windows.ApplicationModel.Background;
using Windows.Storage;
using Windows.Devices.Geolocation;

namespace BackgroundTask
{
    public sealed class LocationBackgroundTask : IBackgroundTask
    {
        private CancellationTokenSource _cts = null;

        async void IBackgroundTask.Run(IBackgroundTaskInstance taskInstance)
        {
            BackgroundTaskDeferral deferral = taskInstance.GetDeferral();

            try
            {
                // Associate a cancellation handler with the background task.
                taskInstance.Canceled += funcOnCanceled;

                // Get cancellation token
                if (_cts == null)
                {
                    _cts = new CancellationTokenSource();
                }
                CancellationToken token = _cts.Token;

                // Create geolocator object
                Geolocator geolocator = new Geolocator();

                // Make the request for the current position
                Geoposition pos = await geolocator.GetGeopositionAsync().AsTask(token);

                DateTime currentTime = DateTime.Now;

                funcWriteStatusToAppData("Time: " + currentTime.ToString());
                funcWriteGeolocToAppData(pos);
            }
            catch (UnauthorizedAccessException)
            {
                funcWriteStatusToAppData("Disabled");
                funcWipeGeolocDataFromAppData();
            }
            catch (Exception ex)
            {
                funcWriteStatusToAppData(ex.ToString());
                funcWipeGeolocDataFromAppData();
            }
            finally
            {
                _cts = null;
                deferral.Complete();
            }
        }

        private void funcWriteGeolocToAppData(Geoposition pos_)
        {
            var settings = ApplicationData.Current.LocalSettings;
            settings.Values["Latitude"] = pos_.Coordinate.Point.Position.Latitude.ToString();
            settings.Values["Longitude"] = pos_.Coordinate.Point.Position.Longitude.ToString();
            settings.Values["Accuracy"] = pos_.Coordinate.Accuracy.ToString();
        }

        private void funcWipeGeolocDataFromAppData()
        {
            var settings = ApplicationData.Current.LocalSettings;
            settings.Values["Latitude"] = "";
            settings.Values["Longitude"] = "";
            settings.Values["Accuracy"] = "";
        }

        private void funcWriteStatusToAppData(string status_)
        {
            var settings = ApplicationData.Current.LocalSettings;
            settings.Values["Status"] = status_;
        }

        private void funcOnCanceled(IBackgroundTaskInstance sender_, BackgroundTaskCancellationReason reason_)
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts = null;
            }
        }
    }
}
