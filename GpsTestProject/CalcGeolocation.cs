using System;
using Windows.Devices.Geolocation;

namespace GpsTestProject
{
    public class GeoPositioning
    {
        /// <summary>
        /// 【角度】 → 【ラジアン】
        /// </summary>
        /// <param name="angle_">角度[°]</param>
        /// <returns></returns>
        static public double FuncAng2Rad(double angle_)
        {
            return ((angle_ * Math.PI) / 180);
        }

        /// <summary>
        /// 【ラジアン】 → 【角度】
        /// </summary>
        /// <param name="radian_">ラジアン</param>
        /// <returns></returns>
        static public double FuncRad2Ang(double radian_)
        {
            return ((radian_ * 180) / Math.PI);
        }
    }
    


    public class GeoPointDistance
    {
        static double EQUATOR_RADIUS = 6378136.6;      //  地球の赤道半径 ：6,378,136.6 [m]
        static double POLOR_RADIUS = 6356752.314;      //  地球の極半径   ：6,356,752.314 [m]
        double LAT_P_METER = 360 / (2 * Math.PI * POLOR_RADIUS);    //  経度／m

        public double lat_p_cm = 0;         //  緯度／㎝
        public double lon_p_cm = 0;         //  経線／cm
        public double lat_pos = 0;          //  緯度＜指定位置＞
        public double lon_pos = 0;          //  経度＜指定位置＞

        public GeoPointDistance(double lat_now_)
        {
            //  緯度／cm
            lat_p_cm = LAT_P_METER / 100;

            //  現在位置緯度での断面半径
            double now_section_radius = EQUATOR_RADIUS * Math.Cos(GeoPositioning.FuncAng2Rad(lat_now_));

            //  現在位置緯度での円周
            double now_lat_circum = 2 * Math.PI * now_section_radius;

            //  経度／m
            double now_lon_p_meter = 360 * now_lat_circum;
            //  経度／cm
            double lon_p_cm = now_lat_circum / 100;
        }

        public GeoPointDistance(BasicGeoposition now_, double angle_, double distance_)
        {
            //  ※※※※※※※※※※※※※※※※※※※※※※※
            //  ＜緯度＞
            //  ※※※※※※※※※※※※※※※※※※※※※※※

            //  緯線上の移動距離
            double lat_distance = distance_ * Math.Cos(GeoPositioning.FuncAng2Rad(angle_));

            //  緯度／cm
            lat_p_cm = LAT_P_METER / 100;

            //  緯度の変化量
            double lat_delta = lat_distance * lat_p_cm;

            //  緯度
            lat_pos = now_.Latitude + lat_delta;

            //  ※※※※※※※※※※※※※※※※※※※※※※※
            //  ＜経度＞
            //  ※※※※※※※※※※※※※※※※※※※※※※※

            //  経線上の移動距離
            double lon_distance = distance_ * Math.Sin(GeoPositioning.FuncAng2Rad(angle_));

            //  経度／cm
            double now_section_radius = EQUATOR_RADIUS * Math.Cos(GeoPositioning.FuncAng2Rad(now_.Latitude));   //  現在位置緯度での断面半径
            double now_lat_circum = 2 * Math.PI * now_section_radius;                                           //  現在位置緯度での円周
            lon_p_cm = 360 * now_lat_circum / 100;

            //  経度の変化量
            double lon_delta = lon_distance * lon_p_cm;

            //  経度
            lon_pos = now_.Longitude + lon_delta;
        }
    }
}