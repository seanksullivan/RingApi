﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.IO;
using System.Net;

namespace KoenZomers.Ring.Api
{
    public class Session
    {
        #region Properties

        /// <summary>
        /// Username to use to connect to the Ring API. Set by providing it in the constructor.
        /// </summary>
        public string Username { get; private set; }

        /// <summary>
        /// Password to use to connect to the Ring API. Set by providing it in the constructor.
        /// </summary>
        public string Password { get; private set; }

        /// <summary>
        /// Returns the Base64 Encoded username and password to use in the authenticate header
        /// </summary>
        public string CredentialsEncoded => Convert.ToBase64String(Encoding.UTF8.GetBytes($"{Username}:{Password}"));

        /// <summary>
        /// Base Uri with which all Ring API requests start
        /// </summary>
        public Uri RingApiBaseUrl => new Uri("https://api.ring.com/clients_api/");

        /// <summary>
        /// Boolean indicating if the current session is authenticated
        /// </summary>
        public bool IsAuthenticated => !string.IsNullOrEmpty(AuthenticationToken);

        /// <summary>
        /// Authentication Token that will be used to communicate with the Ring API
        /// </summary>
        public string AuthenticationToken { get; private set; }

        /// <summary>
        /// Utilized to support unit test via Moq (mocking).
        /// A mocked HttpWebRequest can be passed-in
        /// </summary>
        public HttpWebRequest Request { get; set; }

        #endregion

            #region Fields



            #endregion

            #region Constructors

            /// <summary>
            /// Initiates a new session to the Ring API
            /// </summary>
        public Session(string username, string password)
        {
            Username = username;
            Password = password;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Authenticates to the Ring API
        /// </summary>
        /// <param name="operatingSystem">Operating system from which this API is accessed. Defaults to 'windows'. Required field.</param>
        /// <param name="hardwareId">Hardware identifier of the device for which this API is accessed. Defaults to 'unspecified'. Required field.</param>
        /// <param name="appBrand">Device brand for which this API is accessed. Defaults to 'ring'. Optional field.</param>
        /// <param name="deviceModel">Device model for which this API is accessed. Defaults to 'unspecified'. Optional field.</param>
        /// <param name="deviceName">Name of the device from which this API is being used. Defaults to 'unspecified'. Optional field.</param>
        /// <param name="resolution">Screen resolution on which this API is being used. Defaults to '800x600'. Optional field.</param>
        /// <param name="appVersion">Version of the app from which this API is being used. Defaults to '1.3.810'. Optional field.</param>
        /// <param name="appInstallationDate">Date and time at which the app was installed from which this API is being used. By default not specified. Optional field.</param>
        /// <param name="manufacturer">Name of the manufacturer of the product for which this API is being accessed. Defaults to 'unspecified'. Optional field.</param>
        /// <param name="deviceType">Type of device from which this API is being used. Defaults to 'tablet'. Optional field.</param>
        /// <param name="architecture">Architecture of the system from which this API is being used. Defaults to 'x64'. Optional field.</param>
        /// <param name="language">Language of the app from which this API is being used. Defaults to 'en'. Optional field.</param>        
        /// <returns>Session object if the authentication was successful</returns>
        public async Task<Entities.Session> Authenticate(   string operatingSystem = "windows", 
                                                            string hardwareId = "unspecified", 
                                                            string appBrand = "ring", 
                                                            string deviceModel = "unspecified", 
                                                            string deviceName = "unspecified", 
                                                            string resolution = "800x600", 
                                                            string appVersion = "1.3.810",
                                                            DateTime? appInstallationDate = null,
                                                            string manufacturer = "unspecified",
                                                            string deviceType = "tablet",
                                                            string architecture = "x64",
                                                            string language = "en")
        {
            // Check for mandatory parameters
            if (string.IsNullOrEmpty(operatingSystem))
            {
                throw new ArgumentNullException("operatingSystem", "Operating system is mandatory");
            }
            if (string.IsNullOrEmpty(hardwareId))
            {
                throw new ArgumentNullException("hardwareId", "HardwareId system is mandatory");
            }

            // Construct the Form POST fields to send along with the authentication request
            var formFields = new Dictionary<string, string>
            {
                { "device[os]", operatingSystem },
                { "device[hardware_id]", hardwareId }
            };

            // Add optional fields if they have been provided
            if (!string.IsNullOrEmpty(appBrand)) formFields.Add("device[app_brand]", appBrand);
            if (!string.IsNullOrEmpty(deviceModel)) formFields.Add("device[metadata][device_model]", deviceModel);
            if (!string.IsNullOrEmpty(deviceName)) formFields.Add("device[metadata][device_name]", deviceName);
            if (!string.IsNullOrEmpty(resolution)) formFields.Add("device[metadata][resolution]", resolution);
            if (!string.IsNullOrEmpty(appVersion)) formFields.Add("device[metadata][app_version]", appVersion);
            if (appInstallationDate.HasValue) formFields.Add("device[metadata][app_instalation_date]", string.Format("{0:yyyy-MM-dd}+{0:HH}%3A{0:mm}%3A{0:ss}Z", appInstallationDate.Value));
            if (!string.IsNullOrEmpty(manufacturer)) formFields.Add("device[metadata][manufacturer]", manufacturer);
            if (!string.IsNullOrEmpty(deviceType)) formFields.Add("device[metadata][device_type]", deviceType);
            if (!string.IsNullOrEmpty(architecture)) formFields.Add("device[metadata][architecture]", architecture);
            if (!string.IsNullOrEmpty(language)) formFields.Add("device[metadata][language]", language);

            // Make the Form POST request to authenticate
            var response = await HttpUtility.FormPost(  new Uri(RingApiBaseUrl, "session"),
                                                        formFields,
                                                        new System.Collections.Specialized.NameValueCollection
                                                        {
                                                            { "Accept-Encoding", "gzip, deflate" },
                                                            { "X-API-LANG", "en" },
                                                            { "Authorization", $"Basic {CredentialsEncoded}" }
                                                        },
                                                        null,
                                                        Request);

            // Deserialize the JSON result into a typed object
            var session = JsonConvert.DeserializeObject<Entities.Session>(response);
            AuthenticationToken = session.Profile.AuthenticationToken;

            return session;
        }

        /// <summary>
        /// Returns all devices registered with Ring under the current account being used
        /// </summary>
        /// <returns>Devices registered with Ring under the current account</returns>
        public async Task<Entities.Devices> GetRingDevices()
        {
            if(!IsAuthenticated)
            {
                throw new Exceptions.SessionNotAuthenticatedException();
            }

            var response = await HttpUtility.GetContents(new Uri(RingApiBaseUrl, $"ring_devices?auth_token={AuthenticationToken}&api_version=9"), null);

            var devices = JsonConvert.DeserializeObject<Entities.Devices>(response);
            return devices;
        }

        /// <summary>
        /// Returns all events registered for the doorbots
        /// </summary>
        /// <returns>All events triggered by registered doorbots under the current account</returns>
        public async Task<List<Entities.DoorbotHistoryEvent>> GetDoorbotsHistory()
        {
            if (!IsAuthenticated)
            {
                throw new Exceptions.SessionNotAuthenticatedException();
            }

            var response = await HttpUtility.GetContents(new Uri(RingApiBaseUrl, $"doorbots/history?auth_token={AuthenticationToken}&api_version=9"), null);

            var doorbotHistory = JsonConvert.DeserializeObject<List<Entities.DoorbotHistoryEvent>>(response);
            return doorbotHistory;
        }

        /// <summary>
        /// Returns a stream with the recording of the provided Ding Id of a doorbot
        /// </summary>
        /// <param name="doorbotHistoryEvent">The doorbot history event to retrieve the recording for</param>
        /// <returns>Stream containing contents of the recording</returns>
        public async Task<Stream> GetDoorbotHistoryRecording(Entities.DoorbotHistoryEvent doorbotHistoryEvent)
        {
            return await GetDoorbotHistoryRecording(doorbotHistoryEvent.Id);
        }

        /// <summary>
        /// Returns a stream with the recording of the provided Ding Id of a doorbot
        /// </summary>
        /// <param name="dingId">Id of the doorbot history event to retrieve the recording for</param>
        /// <returns>Stream containing contents of the recording</returns>
        public async Task<Stream> GetDoorbotHistoryRecording(string dingId)
        {
            if (!IsAuthenticated)
            {
                throw new Exceptions.SessionNotAuthenticatedException();
            }

            var stream = await HttpUtility.DownloadFile(new Uri(RingApiBaseUrl, $"dings/{dingId}/recording?auth_token={AuthenticationToken}&api_version=9"), null);
            return stream;
        }

        /// <summary>
        /// Saves the recording of the provided Ding Id of a doorbot to the provided location
        /// </summary>
        /// <param name="doorbotHistoryEvent">The doorbot history event to retrieve the recording for</param>
        public async Task GetDoorbotHistoryRecording(Entities.DoorbotHistoryEvent doorbotHistoryEvent, string saveAs)
        {
            await GetDoorbotHistoryRecording(doorbotHistoryEvent.Id, saveAs);
        }

        /// <summary>
        /// Saves the recording of the provided Ding Id of a doorbot to the provided location
        /// </summary>
        /// <param name="dingId">Id of the doorbot history event to retrieve the recording for</param>
        public async Task GetDoorbotHistoryRecording(string dingId, string saveAs)
        {
            using (var stream = await GetDoorbotHistoryRecording(dingId))
            {
                using (var fileStream = File.Create(saveAs))
                {
                    await stream.CopyToAsync(fileStream);
                }
            }
        }

        #endregion
    }
}
