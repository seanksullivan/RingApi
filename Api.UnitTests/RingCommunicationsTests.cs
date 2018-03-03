using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json;
using KoenZomers.Ring.Api;
using KoenZomers.Ring.Api.Entities;
using System.Collections.Generic;

namespace Api.UnitTests
{
    /// <summary>
    /// Unit tests that take advantage of the Moq framework, using mocks of the HttpWebRequest (and response),
    /// to provide testing and debugging abilities without actual communications with the ring.com Rest API.
    /// </summary>
    [TestClass]
    public class RingCommunicationsTests
    {
        #region Public Properties
        public static string Username => "Someone@gmail.com";

        public static string Password => "Blah";

        public static string AuthenticateResponseFilename => "TestData\\AuthenticateResponse.json";

        public static string DevicesResponseFilename => "TestData\\DevicesResponse.json";

        public static byte[] ExpectedAuthenticationResponseBytes { get; set; }

        public static Session ExpectedAuthenticationSession { get; set; }

        public static byte[] ExpectedDevicesResponseBytes { get; set; }

        public static Devices ExpectedDevices { get; set; }
        #endregion

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            // Read-in the AuthenticateResponse.json test data - as a byte array, to be utilized by the mocked HttpWebResponse
            ExpectedAuthenticationResponseBytes = Encoding.UTF8.GetBytes(File.ReadAllText(AuthenticateResponseFilename));

            // Convert the AuthenticationResponse json to a Session object - to be utilized for comparison against returned AuthenticationResponse
            ExpectedAuthenticationSession = JsonConvert.DeserializeObject<Session>(Encoding.UTF8.GetString(ExpectedAuthenticationResponseBytes));

            // Read-in the DevicesResponse.json test data - as a byte array, to be utilized by the mocked HttpWebResponse
            ExpectedDevicesResponseBytes = Encoding.UTF8.GetBytes(File.ReadAllText(DevicesResponseFilename));

            // Convert the DevicesResponse.json to a Session object - to be utilized for comparison against returned DevicesResponse
            ExpectedDevices = JsonConvert.DeserializeObject<Devices>(Encoding.UTF8.GetString(ExpectedDevicesResponseBytes));
        }

        [TestMethod]
        public void Authenticate_ExpectSuccess()
        {
            // ARRANGE

            // Mock the HttpWebRequest and HttpWebResponse (which is within the request)
            var mockHttpWebRequest = CreateMockHttpWebRequest(HttpStatusCode.NotModified, "A-OK", ExpectedAuthenticationResponseBytes);

            // ACT
            var comm = new RingCommunications(Username, Password) { AuthRequest = mockHttpWebRequest };
            var actualAuthenticationSession = comm.Authenticate().GetAwaiter().GetResult();

            //ASSERT
            ObjectCompare(ExpectedAuthenticationSession, actualAuthenticationSession);
        }

        [TestMethod]
        public void Authenticate_VerifyToken()
        {
            // ARRANGE

            // Mock the HttpWebRequest and HttpWebResponse (which is within the request)
            var mockHttpWebRequest = CreateMockHttpWebRequest(HttpStatusCode.NotModified, "A-OK", ExpectedAuthenticationResponseBytes);

            // ACT
            var comm = new RingCommunications(Username, Password) { AuthRequest = mockHttpWebRequest };
            var actualAuthenticationSession = comm.Authenticate().GetAwaiter().GetResult();

            // ASSERT
            Assert.IsTrue(!string.IsNullOrEmpty(actualAuthenticationSession.Profile.AuthenticationToken), "Failed to authenticate");
        }

        [TestMethod]
        public void Authenticate_VerifyCredentialsEncoded()
        {
            // ARRANGE

            // Mock the HttpWebRequest and HttpWebResponse (which is within the request)
            var mockHttpWebRequest = CreateMockHttpWebRequest(HttpStatusCode.NotModified, "A-OK", ExpectedAuthenticationResponseBytes);

            // ACT
            var comm = new RingCommunications(Username, Password) { AuthRequest = mockHttpWebRequest };
            var actualSessionObject = comm.Authenticate().GetAwaiter().GetResult();

            var base64DecodedCredentials = Encoding.UTF8.GetString(Convert.FromBase64String(comm.CredentialsEncoded));
            Assert.AreEqual(base64DecodedCredentials, $"{Username}:{Password}", "Base64 Credential Decoding failed");
        }

        [TestMethod]
        public void GetRingDevices_Verify()
        {
            // ARRANGE

            // Mock the HttpWebRequest and HttpWebResponse (which is within the request)- AUTH
            var mockHttpWebRequestAuth = CreateMockHttpWebRequest(HttpStatusCode.NotModified, "A-OK", ExpectedAuthenticationResponseBytes);

            // Mock the HttpWebRequest and HttpWebResponse (which is within the request)- Devices
            var mockHttpWebRequestDevices = CreateMockHttpWebRequest(HttpStatusCode.NotModified, "A-OK", ExpectedDevicesResponseBytes);

            // ACT
            var comm = new RingCommunications(Username, Password)
            {
                AuthRequest = mockHttpWebRequestAuth,
                DevicesRequest = mockHttpWebRequestDevices
            };

            var actualSessionAuthObject = comm.Authenticate().GetAwaiter().GetResult();

            var actualDevices = comm.GetRingDevices().GetAwaiter().GetResult();
            Assert.IsTrue(actualDevices.Chimes.Count > 0 && actualDevices.Doorbots.Count > 0, "No doorbots and/or chimes returned");
        }

        /// <summary>
        /// Compare Objects and all fields within.
        /// </summary>
        /// <param name="expected"></param>
        /// <param name="actual"></param>
        private static void ObjectCompare(object expected, object actual)
        {
            PropertyInfo[] properties = expected.GetType().GetProperties();
            foreach (PropertyInfo property in properties)
            {
                object expectedValue = property.GetValue(expected, null);
                object actualValue = property.GetValue(actual, null);

                // if the following types exist, let's get recursive, because they contain one or more of their own fields that we must verify.
                if (expectedValue is SessionFeatures || expectedValue is Profile)
                {
                    ObjectCompare(expectedValue, actualValue);
                    break;
                }

                if (expectedValue is IList)
                {
                    CollectionAssert.AreEqual(expectedValue as IList, actualValue as IList);
                }
                else
                {
                    Assert.AreEqual(expectedValue, actualValue, "Property {0}.{1} does not match. Expected: {2} but was: {3}", 
                        property.DeclaringType.Name, property.Name, expectedValue, actualValue);
                }
            }
        }

        /// <summary>
        /// Create a full, Mock object for the HttpWebRequest
        /// </summary>
        /// <param name="httpStatusCode"></param>
        /// <param name="statusDescription"></param>
        /// <param name="responseBytes"></param>
        /// <returns></returns>
        private static HttpWebRequest CreateMockHttpWebRequest(HttpStatusCode httpStatusCode, string statusDescription, byte[] responseBytes)
        {
            var requestBytes = Encoding.ASCII.GetBytes("Blah Blah Blah");
            Stream requestStream = new MemoryStream();
            Stream responseStream = new MemoryStream();

            using (var memStream = new MemoryStream(requestBytes))
            {
                memStream.CopyTo(requestStream);
                requestStream.Position = 0;
            }

            using (var responseMemStream = new MemoryStream(responseBytes))
            {
                responseMemStream.CopyTo(responseStream);
                responseStream.Position = 0;
            }

            var response = new Mock<HttpWebResponse>(MockBehavior.Loose);
            response.Setup(c => c.StatusCode).Returns(httpStatusCode);
            response.Setup(c => c.GetResponseStream()).Returns(responseStream);
            response.Setup(c => c.StatusDescription).Returns(statusDescription);

            var request = new Mock<HttpWebRequest>();
            request.Setup(c => c.GetRequestStreamAsync()).ReturnsAsync(requestStream);
            request.Setup(c => c.RequestUri).Returns(new Uri("https://www.blah.com"));

            request.Setup(s => s.GetResponseAsync()).ReturnsAsync(response.Object);
            return request.Object;
        }

    }
}
