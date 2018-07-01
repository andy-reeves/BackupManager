﻿using System.Runtime.Serialization;

namespace PushbulletSharp.Models.Responses
{
    [DataContract]
    public class Device
    {
        /// <summary>
        /// Gets or sets the iden.
        /// </summary>
        /// <value>
        /// The iden.
        /// </value>
        [DataMember(Name = "iden")]
        public string Iden { get; set; }

        /// <summary>
        /// Gets or sets the created.
        /// </summary>
        /// <value>
        /// The created.
        /// </value>
        [DataMember(Name = "created")]
        public string Created { get; set; }

        /// <summary>
        /// Gets or sets the nickname.
        /// </summary>
        /// <value>
        /// The nickname.
        /// </value>
        [DataMember(Name = "nickname")]
        public string Nickname { get; set; }

        /// <summary>
        /// Gets or sets the modified.
        /// </summary>
        /// <value>
        /// The modified.
        /// </value>
        [DataMember(Name = "modified")]
        public string Modified { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="Device"/> is active.
        /// </summary>
        /// <value>
        ///   <c>true</c> if active; otherwise, <c>false</c>.
        /// </value>
        [DataMember(Name = "active")]
        public bool Active { get; set; }

        /// <summary>
        /// Gets or sets the model.
        /// </summary>
        /// <value>
        /// The model.
        /// </value>
        [DataMember(Name = "model")]
        public string Model { get; set; }

        /// <summary>
        /// Gets or sets the type.
        /// </summary>
        /// <value>
        /// The type.
        /// </value>
        [DataMember(Name = "type")]
        public string Type { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="Device"/> is pushable.
        /// </summary>
        /// <value>
        ///   <c>true</c> if pushable; otherwise, <c>false</c>.
        /// </value>
        [DataMember(Name = "pushable")]
        public bool Pushable { get; set; }

        /// <summary>
        /// Gets or sets the manufacturer.
        /// </summary>
        /// <value>
        /// The manufacturer.
        /// </value>
        [DataMember(Name = "manufacturer")]
        public string Manufacturer { get; set; }

        /// <summary>
        /// Gets or sets the push_token.
        /// </summary>
        /// <value>
        /// The push_token.
        /// </value>
        [DataMember(Name = "push_token")]
        public string PushToken { get; set; }

        /// <summary>
        /// Gets or sets the app_version.
        /// </summary>
        /// <value>
        /// The app_version.
        /// </value>
        [DataMember(Name = "app_version")]
        public int AppVersion { get; set; }

        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            if (!string.IsNullOrWhiteSpace(Nickname))
            {
                return Nickname;
            }

            return base.ToString();
        }
    }
}