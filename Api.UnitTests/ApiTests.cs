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
    [TestClass]
    public class ApiTests
    {
        [TestMethod]
        public void Session_Authenticate_ExpectSuccess()
        {
            // ARRANGE
            var expectedResponseBytes = Encoding.UTF8.GetBytes(File.ReadAllText("TestData\\AuthenticateResponse.json"));
            var expectedSessionObject = JsonConvert.DeserializeObject<KoenZomers.Ring.Api.Entities.Session>(Encoding.UTF8.GetString(expectedResponseBytes));

            // Mock the HttpWebRequest and HttpWebResponse (which is within the request)
            var mockHttpWebRequest = CreateMockHttpWebRequest(HttpStatusCode.NotModified, "A-OK", expectedResponseBytes);

            // ACT
            var session = new RingCommunications("test@test.com", "someinvalidpassword") { Request = mockHttpWebRequest };
            var actualSessionObject = session.Authenticate().GetAwaiter().GetResult();

            //ASSERT
            ObjectCompare(expectedSessionObject, actualSessionObject);
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
                    Assert.AreEqual(expectedValue, actualValue, "Property {0}.{1} does not match. Expected: {2} but was: {3}", property.DeclaringType.Name, property.Name, expectedValue, actualValue);
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
            //var servicePointMock = new Mock<ServicePoint>(MockBehavior.Loose);


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
