using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using NUnit.Framework;
using RestSharp;
using RestSharp.Authenticators;
using RevueCrafters.Models;

namespace RevueCrafters.Tests
{
    [TestFixture]
    public class RevueCraftersApiTests
    {
        private RestClient client = default!;
        private const string BaseUrl = "https://d2925tksfvgq8c.cloudfront.net";
        private const string LoginEmail = "pesho@example.com";
        private const string LoginPassword = "123456";

        private static string? lastCreatedRevueId;
        private static string Email =>
        Environment.GetEnvironmentVariable("REVUE_EMAIL")
        ?? LoginEmail;

        private static string Password =>
        Environment.GetEnvironmentVariable("REVUE_PASSWORD")
        ?? LoginPassword;


        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            var jwtToken = AuthenticateAndGetJwtToken(Email, Password);


            var options = new RestClientOptions(BaseUrl)
            {
                Authenticator = new JwtAuthenticator(jwtToken)
            };

            client = new RestClient(options);
        }


        private static string AuthenticateAndGetJwtToken(string email, string password)
        {
            var temp = new RestClient(BaseUrl);
            var request = new RestRequest("/api/User/Authentication", Method.Post)
            .AddJsonBody(new { email, password });


            var response = temp.Execute(request);
            if (response.StatusCode != HttpStatusCode.OK)
                throw new InvalidOperationException($"Auth failed: {response.StatusCode} {response.Content}");


            var json = JsonSerializer.Deserialize<JsonElement>(response.Content!);
            var token = json.GetProperty("accessToken").GetString();
            if (string.IsNullOrWhiteSpace(token))
                throw new InvalidOperationException("JWT access token missing in response.");


            return token!;
        }

        [Test, Order(1)]
        public void CreateRevue_WithRequiredFields_ShouldReturnOkAndMessage()
        {
            var body = new RevueDTO
            {
                Title = $"Test Revue {Guid.NewGuid():N}",
                Url = string.Empty,
                Description = "Some description here"
            };


            var request = new RestRequest("/api/Revue/Create", Method.Post)
            .AddJsonBody(body);


            var response = client.Execute(request);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), "Expected 200 OK on create.");


            var api = JsonSerializer.Deserialize<ApiResponseDTO>(response.Content!);
            Assert.That(api, Is.Not.Null);
            Assert.That(api!.Msg, Is.EqualTo("Successfully created!"));
        }

        [Test, Order(2)]
        public void GetAllRevues_ShouldReturnNonEmptyArray_AndCaptureLastId()
        {
            var request = new RestRequest("/api/Revue/All", Method.Get);
            var response = client.Execute(request);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), "Expected 200 OK on get all.");

            var items = JsonSerializer.Deserialize<List<ApiResponseDTO>>(response.Content!);
            Assert.That(items, Is.Not.Null);
            Assert.That(items!, Is.Not.Empty);

            lastCreatedRevueId = items!.LastOrDefault()?.RevueId;
        }

        [Test, Order(3)]
        public void EditExistingRevue_ShouldReturnOkAndEditedMessage()
        {
            var edit = new RevueDTO
            {
                Title = "Edited Revue",
                Url = string.Empty,
                Description = "Edited description"
            };


            var request = new RestRequest("/api/Revue/Edit", Method.Put)
            .AddQueryParameter("revueId", lastCreatedRevueId)
            .AddJsonBody(edit);


            var response = client.Execute(request);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), "Expected status code 200 OK.");


            var api = JsonSerializer.Deserialize<ApiResponseDTO>(response.Content!);
            Assert.That(api, Is.Not.Null);
            Assert.That(api!.Msg, Is.EqualTo("Edited successfully"));
        }


        [Test, Order(4)]
        public void DeleteExistingRevue_ShouldReturnOkAndDeletedMessage()
        {
            var request = new RestRequest("/api/Revue/Delete", Method.Delete)
            .AddQueryParameter("revueId", lastCreatedRevueId);


            var response = client.Execute(request);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), "Expected status code 200 OK.");


            var api = JsonSerializer.Deserialize<ApiResponseDTO>(response.Content!);
            Assert.That(api, Is.Not.Null);
            Assert.That(api!.Msg, Is.EqualTo("The revue is deleted!"));
        }

        [Test, Order(5)]
        public void CreateRevue_WithoutRequiredFields_ShouldReturnBadRequest()
        {
            var bad = new RevueDTO
            {
                Title = string.Empty,
                Description = string.Empty,
                Url = string.Empty
            };
            var request = new RestRequest("/api/Revue/Create", Method.Post).AddJsonBody(bad);
            var response = client.Execute(request);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest), "Expected status code 400 BadRequest.");
        }

        [Test, Order(6)]
        public void EditNonExistingRevue_ShouldReturnBadRequest_AndMessage()
        {
            string fakeId = "123";
            var edit = new RevueDTO
            {
                Title = "Edited Revue",
                Url = string.Empty,
                Description = "Edited description"
            };


            var request = new RestRequest("/api/Revue/Edit", Method.Put)
            .AddQueryParameter("revueId", fakeId)
            .AddJsonBody(edit);


            var response = client.Execute(request);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest), "Expected status code 400 BadRequest.");
            Assert.That(response.Content, Does.Contain("There is no such revue!"));
        }

        [Test, Order(7)]
        public void DeleteNonExistingRevue_ShouldReturnBadRequest_AndMessage()
        {
            string fakeId = "123";
            var request = new RestRequest("/api/Revue/Delete", Method.Delete)
            .AddQueryParameter("revueId", fakeId);

            var response = client.Execute(request);
            Console.WriteLine(response.StatusCode);
            Console.WriteLine(response.Content);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest), "Expected status code 400 BadRequest.");
            Assert.That(response.Content, Does.Contain("There is no such revue!"));
        }


        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            client?.Dispose();
        }
    }
}