﻿using System;
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
using Windows.UI;
using Windows.Storage.Streams;

// 空白ページの項目テンプレートについては、https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x411 を参照してください

namespace GpsTestProject
{
    public enum eNotifyType
    {
        StatusMessage,
        ErrorMessage,
        InitState
    };

    public enum eLRCside
    {
        Left,
        Right,
        Center
    };

    /// <summary>
    /// それ自体で使用できる空白ページまたはフレーム内に移動できる空白ページ。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        // Proides access to location data
        private Geolocator _geolocator = null;

        //  中心点用マップレイヤー
        private MapElementsLayer _map_elm_lyr_icon_center = new MapElementsLayer();
        //  中心軌跡用座標リスト
        private List<BasicGeoposition> _lst_geopoint_line_center = new List<BasicGeoposition>();
        private MapElementsLayer _map_elm_lyr_line_center = new MapElementsLayer();

        //  左右軌跡用
        private bool _bilateral_trace_start = false;

        //  右側用マップレイヤー
        private MapElementsLayer _map_elm_lyr_icon_right = new MapElementsLayer();
        //  右側軌跡用座標リスト
        private List<BasicGeoposition> _lst_geopoint_line_right = new List<BasicGeoposition>();
        private MapElementsLayer _map_elm_lyr_line_right = new MapElementsLayer();

        //  左側用マップレイヤー
        private MapElementsLayer _map_elm_lyr_icon_left = new MapElementsLayer();
        //  左側軌跡用座標リスト
        private List<BasicGeoposition> _lst_geopoint_line_left = new List<BasicGeoposition>();
        private MapElementsLayer _map_elm_lyr_line_left = new MapElementsLayer();

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
                    _geolocator = new Geolocator {
                        MovementThreshold = 0.1,
                        ReportInterval = 0,                         //  リアルタイムの場合は「0」
                        DesiredAccuracy = PositionAccuracy.High,    //  要求精度＝高
                        DesiredAccuracyInMeters = 0,
                    };

                    //  イベント登録  ※トラッキングポジション更新用
                    _geolocator.PositionChanged += EvtOnPositionChanged;

                    //  イベント登録  ※ロケーション状態更新用
                    _geolocator.StatusChanged += EvtOnStatusChanged;

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

            txBk_状態.Text = "停止";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender_"></param>
        /// <param name="e_"></param>
        private void EvtChkBx_DispTrack_Click(object sender_, RoutedEventArgs e_)
        {
            _map_elm_lyr_icon_center.MapElements.Clear();
            FuncAddMapIcon(gpsMap.Center, true, eLRCside.Center);
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

                //  地図内の情報更新
                FuncUpdateMapInfo(pos_.Coordinate.Point);
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

            gpsMap.Layers.Add(_map_elm_lyr_icon_center);
            gpsMap.Layers.Add(_map_elm_lyr_line_center);
            gpsMap.Layers.Add(_map_elm_lyr_icon_right);
            gpsMap.Layers.Add(_map_elm_lyr_line_right);
            gpsMap.Layers.Add(_map_elm_lyr_icon_left);
            gpsMap.Layers.Add(_map_elm_lyr_line_left);
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
                //  地図内の情報更新
                FuncUpdateMapInfo(args_.Location);
            }
        }

        #endregion デバッグメニュー

        #region マップ要素更新
        private void FuncUpdateMapInfo(Geopoint pos_)
        {
            //  地図位置更新
            gpsMap.Center = pos_;

            //  マップアイコン追加
            FuncAddMapIconChecked(pos_, chkBx_中心点表示.IsChecked, eLRCside.Center);

            //  軌跡追加
            _lst_geopoint_line_center.Add(pos_.Position);
            FuncAddMapLine(_lst_geopoint_line_center, eLRCside.Center);

            //  進行角度
            double phi = FuncCalcRelativeAngle(_lst_geopoint_line_center);
            FuncUpdateRelativeAngle(phi);

            if (true == chkBx_左幅指定.IsChecked)
            {
                BasicGeoposition pos_left = FuncCalcLocatioPos(pos_.Position, phi, eLRCside.Left);
            }

            if (true == chkBx_右幅指定.IsChecked)
            {
                BasicGeoposition pos_right = FuncCalcLocatioPos(pos_.Position, phi, eLRCside.Right);
            }
        }

        /// <summary>
        /// マップアイコン更新（チェックボックスのチェック）
        /// </summary>
        /// <param name="pos_"></param>
        /// <param name="chked_"></param>
        private void FuncAddMapIconChecked(Geopoint pos_, bool? chked_, eLRCside side_)
        {
            bool update = false;
            if (chked_.HasValue && chked_.Value)
            {
                update = true;
            }

            FuncAddMapIcon(pos_, update, side_);
        }

        /// <summary>
        /// マップアイコン更新
        /// </summary>
        /// <param name="pos_"></param>
        /// <param name="update_"></param>
        /// <param name="side_"></param>
        private void FuncAddMapIcon(Geopoint pos_, bool update_, eLRCside side_)
        {
            if (update_)
            {
                //  マップアイコンレイヤーに追加
                MapIcon map_icon = new MapIcon
                {
                    Location = pos_,
                    NormalizedAnchorPoint = new Point(0.5, 0.5),
                    Image = RandomAccessStreamReference.CreateFromUri(new Uri("ms-appx:///Assets/point_red.png")),

                    ZIndex = 0
                };

                switch (side_)
                {
                    case eLRCside.Left:
                        _map_elm_lyr_icon_left.MapElements.Add(map_icon);
                        break;
                    case eLRCside.Right:
                        _map_elm_lyr_icon_right.MapElements.Add(map_icon);
                        break;
                    case eLRCside.Center:
                    default:
                        _map_elm_lyr_icon_center.MapElements.Add(map_icon);
                        break;
                }
            }
        }

        /// <summary>
        /// マップライン更新
        /// </summary>
        private void FuncAddMapLine(List<BasicGeoposition> lst_pos_, eLRCside side_)
        {
            //+++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
            //  2点間でラインを引くため、2点より少ない場合は処理しない
            if (lst_pos_.Count <= 1)
            {
                return;
            }

            //+++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
            //  チェックがない場合は処理しない
            if (true != chkBx_軌跡表示.IsChecked)
            {
                return;
            }

            //  位置リスト設定
            List<BasicGeoposition> lst_geo_pos = new List<BasicGeoposition>
            {
                //  前回値
                new BasicGeoposition
                {
                    Latitude = lst_pos_[lst_pos_.Count - 2].Latitude,
                    Longitude = lst_pos_[lst_pos_.Count - 2].Longitude,
                    Altitude = 0,
                },

                //  今回値
                new BasicGeoposition
                {
                    Latitude = lst_pos_[lst_pos_.Count - 1].Latitude,
                    Longitude = lst_pos_[lst_pos_.Count - 1].Longitude,
                    Altitude = 0,
                },
            };

            MapPolyline map_line = new MapPolyline
            {
                Path = new Geopath(lst_geo_pos),
                StrokeColor = Colors.Black,
                StrokeThickness = 5,

                ZIndex = 0
            };

            //  線情報追加
            switch (side_)
            {
                case eLRCside.Left:
                    _map_elm_lyr_line_left.MapElements.Add(map_line);
                    break;
                case eLRCside.Right:
                    _map_elm_lyr_line_right.MapElements.Add(map_line);
                    break;
                case eLRCside.Center:
                default:
                    _map_elm_lyr_line_center.MapElements.Add(map_line);
                    break;
            }
        }
        #endregion マップ要素更新

        /// <summary>
        /// 移動方向の方位角
        /// 北：0°
        /// 東：90°
        /// 南：180°
        /// 西：270°
        /// </summary>
        /// <param name="lst_pos_"></param>
        /// <returns></returns>
        private double FuncCalcRelativeAngle(List<BasicGeoposition> lst_pos_)
        {
            //+++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
            //  2点間で角度を計算ため、2点より少ない場合は処理しない
            if (lst_pos_.Count <= 1)
            {
                return 0;
            }

            //  ※※※※※※※※※※※※※※※※※※※※※※※
            //  三角関数で計算する場合、角度→ラジアンへ変換
            //  ※※※※※※※※※※※※※※※※※※※※※※※

            //  地点B（経度[longitude]x2, 緯度[latitude]y2）
            double x2 = GeoPositioning.FuncAng2Rad(lst_pos_[lst_pos_.Count - 1].Longitude);
            double y2 = GeoPositioning.FuncAng2Rad(lst_pos_[lst_pos_.Count - 1].Latitude);

            //  地点A（経度[longitude]x1, 緯度[latitude]y1）
            double x1 = GeoPositioning.FuncAng2Rad(lst_pos_[lst_pos_.Count - 2].Longitude);
            double y1 = GeoPositioning.FuncAng2Rad(lst_pos_[lst_pos_.Count - 2].Latitude);

            double delta_x = x2 - x1;
            double delta_y = y2 - y1;
            //  atan：分子
            double arg_numerator = Math.Sin(delta_x);

            //  atan：分母
            double arg_denominator_1 = Math.Cos(y1) * Math.Tan(y2);
            double arg_denominator_2 = Math.Sin(y1) * Math.Cos(delta_x);
            double arg_denominator = arg_denominator_1 - arg_denominator_2;

            double phi_radian = Math.Atan(arg_numerator / arg_denominator);
            double phi_rod = GeoPositioning.FuncRad2Ang(phi_radian);
            if (delta_y <= 0)
            {
                phi_rod += 180;
            }

            if(phi_rod <= 0)
            {
                phi_rod += 360;
            }

            return phi_rod;
        }

        private BasicGeoposition FuncCalcLocatioPos(BasicGeoposition now_, double angle_head_, eLRCside side_)
        {
            double angle_side = 0;
            double distance = 0;
            
            //  左右の角度調整, 距離
            if(eLRCside.Left == side_)
            {
                distance = double.Parse(txBx_左幅指定.Text);
                angle_side = angle_head_ - 90;
                //  北～西間の場合、東～北間に補正
                if(angle_side < 0)
                {
                    angle_side += 360;
                }
            }
            else
            {
                distance = double.Parse(txBx_右幅指定.Text);
                angle_side = angle_head_ + 90;
                //  東～北間の場合、北～西間に補正
                if (360 < angle_side)
                {
                    angle_side -= 360;
                }
            }

            GeoPointDistance geo_pos = new GeoPointDistance(now_, angle_side, distance);
            return new BasicGeoposition()
            {
                Latitude = geo_pos.lat_pos,
                Longitude = geo_pos.lon_pos,
            };
        }
        

        private void FuncUpdateRelativeAngle(double phi_)
        {
            //  左上矢印の進行方向の角度を更新
            imgArrowProgress.RenderTransform = new RotateTransform()
            {
                Angle = phi_,
            };

            //  中央矢印の進行方向の角度を更新
            imgArrowCenter.RenderTransform = new RotateTransform()
            {
                Angle = phi_,
            };

            txBk_進行角度.Text = phi_.ToString();
        }

        private void FuncUpdateSidePoint(BasicGeoposition now_pos_)
        {
            GeoPointDistance pos_unit = new GeoPointDistance(now_pos_.Latitude);
        }

        /// <summary>
        /// 【イベントハンドラー】
        ///     発生タイミング
        ///     ・"【チェックボックス】左右点軌跡表示"チェック付けた時
        /// </summary>
        /// <param name="sender_"></param>
        /// <param name="e_"></param>
        private void EvtChkBx_BilateralTrace_Checked(object sender_, RoutedEventArgs e_)
        {
            if (!_bilateral_trace_start)
            {
                
            }
        }

        /// <summary>
        /// 【イベントハンドラー】
        ///     発生タイミング
        ///     ・"【チェックボックス】左右点軌跡表示"チェック外した時
        /// </summary>
        /// <param name="sender_"></param>
        /// <param name="e_"></param>
        private void EvtChkBx_BilateralTrace_UnChecked(object sender_, RoutedEventArgs e_)
        {
            _bilateral_trace_start = false;
        }

        /// <summary>
        /// 【イベントハンドラー】
        ///     発生タイミング
        ///     ・"【ページ】メイン"レイアウト変更時
        /// </summary>
        /// <param name="sender_"></param>
        /// <param name="e_"></param>
        private void EvtPage_LayoutUpdated(object sender_, object e_)
        {
            double left = (gpsMap.ActualWidth - imgArrowCenter.ActualWidth) / 2;
            double top = (gpsMap.ActualHeight - imgArrowCenter.ActualHeight) / 2;

            //  マップ中央に表示されるように調整
            Canvas.SetLeft(imgArrowCenter, left);
            Canvas.SetTop(imgArrowCenter, top);
        }
    }
}
