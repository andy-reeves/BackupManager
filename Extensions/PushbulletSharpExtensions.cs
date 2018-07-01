﻿using PushbulletSharp.Models.Responses;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;

namespace PushbulletSharp
{
    public static class PushbulletSharpExtensions
    {
        /// <summary>
        /// To the json.
        /// </summary>
        /// <param name="data">The data.</param>
        /// <returns></returns>
        public static string ToJson(this object data)
        {
            var serializer = new DataContractJsonSerializer(data.GetType());

            using (MemoryStream stream = new MemoryStream())
            {
                serializer.WriteObject(stream, data);
                return Encoding.UTF8.GetString(stream.ToArray(), 0, (int)stream.Length);
            }
        }

        /// <summary>
        /// Jsons to ojbect.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="json">The json.</param>
        /// <returns></returns>
        public static T JsonToOjbect<T>(this string json)
        {
            var bytes = Encoding.Unicode.GetBytes(json);
            using (MemoryStream stream = new MemoryStream(bytes))
            {
                var serializer = new DataContractJsonSerializer(typeof(T));
                var output = (T)serializer.ReadObject(stream);
                return output;
            }
        }

        /// <summary>
        /// Unixes the time to date time.
        /// </summary>
        /// <param name="unixTime">The unix time.</param>
        /// <returns></returns>
        public static DateTime UnixTimeToDateTime(this string unixTime)
        {
            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            double seconds = double.Parse(unixTime, CultureInfo.InvariantCulture);
            return epoch.AddSeconds(seconds);
        }

        /// <summary>
        /// Dates the time to unix time (at UTC timezone).
        /// </summary>
        /// <param name="dateTime">The date time.</param>
        /// <returns></returns>
        public static double DateTimeToUnixTime(this DateTime dateTime)
        {
            var epochTime = new DateTime(1970, 1, 1);
            var utcTime = TimeZoneInfo.ConvertTime(dateTime, TimeZoneInfo.Utc);
            return (utcTime - epochTime).TotalSeconds;
        }

        /// <summary>
        /// Dates the time to unix time (at UTC timezone).
        /// </summary>
        /// <param name="dateTime">The date time.</param>
        /// <returns></returns>
        public static double DateTimeToUnixTime(this DateTime? dateTime)
        {
            if(dateTime != null)
            {
                DateTime nonNullDateTime = dateTime ?? DateTime.Now;
                return nonNullDateTime.DateTimeToUnixTime();
            }
            else
            {
                return DateTime.Now.DateTimeToUnixTime();
            }
        }

        /// <summary>
        /// Converts the basic push response.
        /// </summary>
        /// <param name="basicResponse">The basic response.</param>
        /// <returns></returns>
        public static PushResponse ConvertBasicPushResponse(BasicPushResponse basicResponse)
        {
            PushResponse response = new PushResponse();
            response.Active = basicResponse.Active;
            response.Created = TimeZoneInfo.ConvertTime(basicResponse.Created.UnixTimeToDateTime(), TimeZoneInfo.Utc);
            response.Dismissed = basicResponse.Dismissed;
            response.Direction = basicResponse.Direction;
            response.Iden = basicResponse.Iden;
            response.Modified = TimeZoneInfo.ConvertTime(basicResponse.Modified.UnixTimeToDateTime(), TimeZoneInfo.Utc);
            response.ReceiverEmail = basicResponse.ReceiverEmail;
            response.ReceiverEmailNormalized = basicResponse.ReceiverEmailNormalized;
            response.ReceiverIden = basicResponse.ReceiverIden;
            response.SenderEmail = basicResponse.SenderEmail;
            response.SenderEmailNormalized = basicResponse.SenderEmailNormalized;
            response.SenderIden = basicResponse.SenderIden;
            response.SenderName = basicResponse.SenderName;
            response.SourceDeviceIden = basicResponse.SourceDeviceIden;
            response.TargetDeviceIden = basicResponse.TargetDeviceIden;
            response.Type = ConvertPushResponseType(basicResponse.Type);
            response.ClientIden = basicResponse.ClientIden;
            response.Title = basicResponse.Title;
            response.Body = basicResponse.Body;
            response.Url = basicResponse.Url;
            response.FileName = basicResponse.FileName;
            response.FileType = basicResponse.FileType;
            response.FileUrl = basicResponse.FileUrl;
            response.ImageUrl = basicResponse.ImageUrl;
            response.Name = basicResponse.Name;
            response.Address = basicResponse.Address;
            if (basicResponse.Items != null)
            {
                response.Items = basicResponse.Items.Select(o => new ListItem() { Checked = o.Checked, Text = o.Text }).ToList();
            }
            return response;
        }


        /// <summary>
        /// Converts the type of the push response.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns></returns>
        private static PushResponseType ConvertPushResponseType(string type)
        {
            switch (type)
            {
                case PushbulletConstants.TypeConstants.Address:
                    return PushResponseType.Address;
                case PushbulletConstants.TypeConstants.File:
                    return PushResponseType.File;
                case PushbulletConstants.TypeConstants.Link:
                    return PushResponseType.Link;
                case PushbulletConstants.TypeConstants.List:
                    return PushResponseType.List;
                case PushbulletConstants.TypeConstants.Note:
                default:
                    return PushResponseType.Note;
            }
        }
    }
}