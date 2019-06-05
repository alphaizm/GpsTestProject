using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Devices.Geolocation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.ApplicationModel;
using Windows.UI.Xaml.Controls.Maps;

// 空白ページの項目テンプレートについては、https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x411 を参照してください

namespace GpsTestProject
{
    public enum eNotifyType
    {
        StatusMessage,
        ErrorMessage,
        InitState
    };

    /// <summary>
    /// それ自体で使用できる空白ページまたはフレーム内に移動できる空白ページ。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        // Proides access to location data
        private Geolocator _geolocator = null;

        public MainPage()
        {
            this.InitializeComponent();
            Application.Current.Suspending += new SuspendingEventHandler(EvtSuspending);
        }

        /// <summary>
        /// 【イベントハンドラー】
        ///     発生タイミング
        ///     ・アプリ終了時
        /// </summary>
        /// <param name="sender_"></param>
        /// <param name="e_"></param>
        private void EvtSuspending(object sender_, SuspendingEventArgs e_)
        {
            //  トラッキング停止
            FuncStopGeolocatorTracking();
        }

        #region トラッキング制御
        /// <summary>
        /// 【イベントハンドラー】
        ///     発生タイミング
        ///     ・"【トラッキング】開始"押下時
        /// </summary>
        /// <param name="sender_"></param>
        /// <param name="e_"></param>
        private async void EvtBtnStartTracking_Click(object sender_, RoutedEventArgs e_)
        {
            // Request permission to accses location
            var access_status = await Geolocator.RequestAccessAsync();

            switch (access_status)
            {
                case GeolocationAccessStatus.Allowed:
                    //  イベント設定前に以下のどちらかを設定する必要あり
                    //      ・定期毎    → ReportInterval [ms]
                    //      ・距離毎    → MovementThreshold [m]
                    _geolocator = new Geolocator { MovementThreshold = 0.5 };

                    //  イベント登録  ※トラッキングポジション更新用
                    _geolocator.PositionChanged += EvtOnPositionChanged;

                    //  イベント登録  ※ロケーション状態更新用
                    _geolocator.StatusChanged += EvtOnStatusChanged;

                    //  求める精度のレベルを「高」
                    _geolocator.DesiredAccuracy = PositionAccuracy.High;

                    FuncNotifyUser("更新待機中...", eNotifyType.StatusMessage);

                    txBk_位置情報無効時説明.Visibility = Visibility.Collapsed;

                    //  ボタン状態更新
                    btnTrackingStart.IsEnabled = false;
                    btnTrackingStop.IsEnabled = true;
                    break;

                case GeolocationAccessStatus.Denied:
                    FuncNotifyUser("位置情報へのアクセス拒否", eNotifyType.ErrorMessage);
                    txBk_位置情報無効時説明.Visibility = Visibility.Visible;
                    break;

                case GeolocationAccessStatus.Unspecified:
                default:
                    FuncNotifyUser("想定外エラー!!", eNotifyType.ErrorMessage);
                    txBk_位置情報無効時説明.Visibility = Visibility.Collapsed;
                    break;
            }
        }

        /// <summary>
        /// 【イベントハンドラー】
        ///     発生タイミング
        ///     ・"【トラッキング】終了"押下時
        /// </summary>
        /// <param name="sender_"></param>
        /// <param name="e_"></param>
        private void EvtBtnStopTracking_Click(object sender_, RoutedEventArgs e_)
        {
            //  トラッキング停止
            FuncStopGeolocatorTracking();
        }

        /// <summary>
        /// 【イベントハンドラー】
        ///     発生タイミング
        ///     ・特定のトラッキングセッションのための場所が有効だった場合
        /// </summary>
        /// <param name="sender_">Geolocator instance</param>
        /// <param name="e_">Position data</param>
        async private void EvtOnPositionChanged(Geolocator sender_, PositionChangedEventArgs e_)
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                txBk_状態.Text = "位置情報取得中";
                FuncNotifyUser("位置情報更新", eNotifyType.StatusMessage);
                FuncUpdateLocationData(e_.Position);
            });
        }

        /// <summary>
        /// 【イベントハンドラー】
        ///     発生タイミング
        ///     ・システム上のロケーション状態が変化した場合
        /// </summary>
        /// <param name="sender_">Geolocator instance</param>
        /// <param name="e_">Status data</param>
        async private void EvtOnStatusChanged(Geolocator sender_, StatusChangedEventArgs e_)
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                //  位置情報設定は、無効時のみ表示
                txBk_位置情報無効時説明.Visibility = Visibility.Collapsed;

                switch (e_.Status)
                {
                    case PositionStatus.Ready:
                        txBk_状態.Text = "準備中";
                        FuncNotifyUser("位置情報構成の準備中", eNotifyType.StatusMessage);
                        break;
                    case PositionStatus.Initializing:
                        txBk_状態.Text = "初期化中";
                        FuncNotifyUser("位置情報構成で位置取得中", eNotifyType.StatusMessage);
                        break;
                    case PositionStatus.NoData:
                        txBk_状態.Text = "データなし";
                        FuncNotifyUser("有効な位置情報を確定できず", eNotifyType.ErrorMessage);
                        break;
                    case PositionStatus.Disabled:
                        txBk_状態.Text = "無効";
                        FuncNotifyUser("位置情報へのアクセス拒否", eNotifyType.ErrorMessage);

                        txBk_位置情報無効時説明.Visibility = Visibility.Visible;

                        //  cached location data クリア
                        FuncUpdateLocationData(null);
                        break;
                    case PositionStatus.NotInitialized:
                        txBk_状態.Text = "未初期化";
                        FuncNotifyUser("位置情報への取得要求は未作成", eNotifyType.StatusMessage);
                        break;
                    case PositionStatus.NotAvailable:
                        txBk_状態.Text = "有効なし";
                        FuncNotifyUser("本OSバージョンでは位置情報は無効", eNotifyType.ErrorMessage);
                        break;
                    default:
                        txBk_状態.Text = "不明";
                        FuncNotifyUser("", eNotifyType.ErrorMessage);
                        break;
                }
            });
        }

        /// <summary>
        /// 【内部関数】
        ///     
        /// </summary>
        private void FuncStopGeolocatorTracking()
        {
            if (null != _geolocator)
            {
                //  イベント解除
                _geolocator.PositionChanged -= EvtOnPositionChanged;
                _geolocator.StatusChanged -= EvtOnStatusChanged;
                _geolocator = null;
            }

            //  ボタン状態更新
            btnTrackingStart.IsEnabled = true;
            btnTrackingStop.IsEnabled = false;

            //  メッセージ初期化
            FuncNotifyUser("", eNotifyType.InitState);
        }



        /// <summary>
        /// 【内部関数】
        ///     ユーザーにメッセージ表示
        ///     【注意】本メソッドは他スレッドから呼ばれる可能性あり
        /// </summary>
        /// <param name="strMessage_"></param>
        /// <param name="type_"></param>
        private void FuncNotifyUser(string strMessage_, eNotifyType type_)
        {
            //  UIスレッドから呼ばれた場合、即反映
            if (Dispatcher.HasThreadAccess)
            {
                FuncUpdateStatus(strMessage_, type_);
            }
            else
            {
                var task = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => FuncUpdateStatus(strMessage_, type_));
            }
        }

        /// <summary>
        /// 【内部関数】
        ///     ステータスメッセージを更新
        /// </summary>
        /// <param name="strMessage_"></param>
        /// <param name="type_"></param>
        private void FuncUpdateStatus(string strMessage_, eNotifyType type_)
        {
            switch (type_)
            {
                case eNotifyType.StatusMessage:
                    StatusBorder.Background = new SolidColorBrush(Windows.UI.Colors.Green);
                    break;
                case eNotifyType.ErrorMessage:
                    StatusBorder.Background = new SolidColorBrush(Windows.UI.Colors.Red);
                    break;
                case eNotifyType.InitState:
                default:
                    StatusBorder.Background = new SolidColorBrush(new UISettings().GetColorValue(desiredColor: UIColorType.Background));
                    break;
            }

            StatusBlock.Text = ("" != strMessage_) ? strMessage_ : "ＧＰＳ非動作";

            //  ステータス更新のためにスクリーンリーダーにイベント発生
            //var peer = FrameworkElementAutomationPeer.FromElement(StatusBlock);
            //if (peer != null)
            //{
            //    peer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
            //}
        }

        /// <summary>
        /// 【内部関数】
        /// </summary>
        /// <param name="pos_"></param>
        private void FuncUpdateLocationData(Geoposition pos_)
        {
            if (null == pos_)
            {
                txBk_緯度.Text = "No data";
                txBk_経度.Text = "No data";
                txBk_精度.Text = "No data";
            }
            else
            {
                txBk_緯度.Text = pos_.Coordinate.Point.Position.Latitude.ToString();
                txBk_経度.Text = pos_.Coordinate.Point.Position.Longitude.ToString();
                txBk_精度.Text = pos_.Coordinate.Accuracy.ToString();

                //  地図位置更新
                gpsMap.Center = pos_.Coordinate.Point;

                MapIcon map_icon = new MapIcon
                {
                    Location = gpsMap.Center,
                    NormalizedAnchorPoint = new Point(0.5, 0.5),

                    ZIndex = 0
                };

                gpsMap.MapElements.Add(map_icon);

            }
        }
        #endregion トラッキング制御

        #region 地図制御
        private void EvtGpsMap_Loaded(object sender_, RoutedEventArgs e_)
        {
            //  初期位置は、中海小学校
            gpsMap.Center = new Geopoint(new BasicGeoposition() { Latitude = 36.397668, Longitude = 136.518854 });
            gpsMap.ZoomLevel = 18;
            FuncSetMapStyle();
            FuncSetMapProjection();

            MapIcon map_icon = new MapIcon
            {
                Location = gpsMap.Center,
                NormalizedAnchorPoint = new Point(0.5, 0.5),
                Title = "first pos",

                ZIndex = 0
            };

            gpsMap.MapElements.Add(map_icon);
        }

        private void EvtCmbxStyle_SelectionChanged(object sender_, SelectionChangedEventArgs e_)
        {
            // Protect against events that are raised before we are fully initialized.
            if (null != gpsMap)
            {
                FuncSetMapStyle();
            }
        }

        private void EvtCmbxProjection_SelectionChanged(object sender_, SelectionChangedEventArgs e_)
        {
            // Protect against events that are raised before we are fully initialized.
            if (null != gpsMap)
            {
                FuncSetMapProjection();
            }
        }

        private void FuncSetMapStyle()
        {
            switch (cmbxStyle.SelectedIndex)
            {
                case 0:
                    gpsMap.Style = MapStyle.None;
                    break;
                case 1:
                    gpsMap.Style = MapStyle.Road;
                    break;
                case 2:
                    gpsMap.Style = MapStyle.Aerial;
                    break;
                case 3:
                    gpsMap.Style = MapStyle.AerialWithRoads;
                    break;
                case 4:
                    gpsMap.Style = MapStyle.Terrain;
                    break;
                default:
                    gpsMap.Style = MapStyle.None;
                    break;
            }
        }

        private void FuncSetMapProjection()
        {
            switch (cmbxProjection.SelectedIndex)
            {
                case 0:
                    gpsMap.MapProjection = MapProjection.WebMercator;
                    break;
                case 1:
                    gpsMap.MapProjection = MapProjection.Globe;
                    break;
                default:
                    gpsMap.MapProjection = MapProjection.WebMercator;
                    break;
            }
        }

        #endregion 地図制御

        #region デバッグメニュー
        /// <summary>
        /// 【イベントハンドラー】
        ///     発生タイミング
        ///     ・"【ハンバーガー】メニュー"トグル押下時
        /// </summary>
        /// <param name="sender_"></param>
        /// <param name="e_"></param>
        private void EvtTggBtn_Click(object sender_, RoutedEventArgs e_)
        {
            Splitter.IsPaneOpen = !Splitter.IsPaneOpen;
        }

        private void EvtChkBxChgDbgMap_Checked(object sender_, RoutedEventArgs e_)
        {
            //  トラッキング停止
            FuncStopGeolocatorTracking();

            //  cached location data クリア
            FuncUpdateLocationData(null);
        }

        private void EvtDbgMap_Loaded(object sender_, RoutedEventArgs e_)
        {
            //  初期位置は、中海小学校
            dbgMap.Center = new Geopoint(new BasicGeoposition() { Latitude = 36.397668, Longitude = 136.518854 });
            dbgMap.ZoomLevel = 18;
            dbgMap.Style = MapStyle.Aerial;
            dbgMap.MapProjection = MapProjection.WebMercator;
        }

        /// <summary>
        /// 【イベントハンドラー】
        ///     発生タイミング
        ///     ・"【デバッグマップ】"右タップ時
        private void EvtDbgMap_MapRightTapped(MapControl sender_, MapRightTappedEventArgs args_)
        {
            if (true == chkBx_デバッグ用マップ使用切り替え.IsChecked)
            {
                gpsMap.Center = args_.Location;
            }
        }

        #endregion デバッグメニュー
    }
}
