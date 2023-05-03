using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Azure.AI.OpenAI;
using Azure;
using System.Collections.Generic;
using MongoDB.Driver;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace EnterpriseAILib
{
    public static class OpenAIFuncs
    {
        private static IMongoCollection<Log> _log;
        private static List<string> prompts = new List<string>();
        [FunctionName("SendPrompt")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            string prompt = req.Query["prompt"];


            var replyText = await GetAnswer(prompt);
            return new OkObjectResult(replyText);
        }

       private static async Task<string> GetAnswer(string question)
        {
            OpenAIClient client = new OpenAIClient(
                new Uri("https://xyz.openai.azure.com/"),
                new AzureKeyCredential("API KEY"));
            GetMongoCollection("Log");
            var log = PopulateLog(question, "REQ");
            await _log.InsertOneAsync(log);
            string promptwithcontext = await GetPrompt(question);
            CompletionsOptions completionsOptions = new CompletionsOptions()
            {
                Prompts = {
                    promptwithcontext
                },
                MaxTokens = 800,
                Temperature = 0.7f
            };
            Completions completionsResponse = client.GetCompletions("gpt35turbo", completionsOptions);
            string completion = completionsResponse.Choices[0].Text;
            log = PopulateLog(completion, "RESP");
            await _log.InsertOneAsync(log);
            return completion;
        }

        private static async Task<string> GetPrompt(string question)
        {
            string promptout = "";
            if (prompts.Count > 10)
            {
                prompts.RemoveAt(0);
            }
            prompts.Add(question);
            foreach (string s in prompts)
            {
                promptout += s;
            }
            return promptout;
        }
        private static  void GetMongoCollection(string Name)
        {
            var client = new MongoClient("Connection String");
            var database = client.GetDatabase("Logs");
            _log = database.GetCollection<Log>(Name);
        }

        private static Log PopulateLog(string LogEntry, string type)
        {
            Log log = new Log();
            log.Type = type;
            log.Message = LogEntry;
            log.actionDate = DateTime.UtcNow;
            return log;
        }

        public class Log
        {
            [BsonId]
            [BsonRepresentation(BsonType.ObjectId)]
            public string Id { get; set; }
            //Request Or Response
            //[BsonElement("Type")]
            public string Type { get; set; }
            //[BsonElement("Message")]
            public string Message { get; set; }
            //[BsonElement("actiondate")]
            public DateTime actionDate { get; set; }
        }
    }
}
