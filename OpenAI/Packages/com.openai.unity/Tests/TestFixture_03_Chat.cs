// Licensed under the MIT License. See LICENSE in the project root for license information.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using OpenAI.Chat;
using OpenAI.Models;
using OpenAI.Tests.Weather;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace OpenAI.Tests
{
    internal class TestFixture_03_Chat : AbstractTestFixture
    {
        [Test]
        public async Task Test_01_01_GetChatCompletion()
        {
            Assert.IsNotNull(OpenAIClient.ChatEndpoint);
            var messages = new List<Message>
            {
                new Message(Role.System, "You are a helpful assistant."),
                new Message(Role.User, "Who won the world series in 2020?"),
                new Message(Role.Assistant, "The Los Angeles Dodgers won the World Series in 2020."),
                new Message(Role.User, "Where was it played?"),
            };
            var chatRequest = new ChatRequest(messages, Model.GPT4);
            var response = await OpenAIClient.ChatEndpoint.GetCompletionAsync(chatRequest);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Choices);
            Assert.IsNotEmpty(response.Choices);

            foreach (var choice in response.Choices)
            {
                Debug.Log($"[{choice.Index}] {choice.Message.Role}: {choice} | Finish Reason: {choice.FinishReason}");
            }

            response.GetUsage();
        }

        [Test]
        public async Task Test_01_02_GetChatStreamingCompletion()
        {
            Assert.IsNotNull(OpenAIClient.ChatEndpoint);
            var messages = new List<Message>
            {
                new Message(Role.System, "You are a helpful assistant."),
                new Message(Role.User, "Who won the world series in 2020?"),
                new Message(Role.Assistant, "The Los Angeles Dodgers won the World Series in 2020."),
                new Message(Role.User, "Where was it played?"),
            };
            var chatRequest = new ChatRequest(messages);
            var cumulativeDelta = string.Empty;
            var response = await OpenAIClient.ChatEndpoint.StreamCompletionAsync(chatRequest, partialResponse =>
            {
                Assert.IsNotNull(partialResponse);
                Assert.NotNull(partialResponse.Choices);
                Assert.NotZero(partialResponse.Choices.Count);

                foreach (var choice in partialResponse.Choices.Where(choice => choice.Delta?.Content != null))
                {
                    cumulativeDelta += choice.Delta.Content;
                }
            });
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Choices);
            var choice = response.FirstChoice;
            Assert.IsNotNull(choice);
            Assert.IsNotNull(choice.Message);
            Assert.IsFalse(string.IsNullOrEmpty(choice.ToString()));
            Debug.Log($"[{choice.Index}] {choice.Message.Role}: {choice} | Finish Reason: {choice.FinishReason}");
            Assert.IsTrue(choice.Message.Role == Role.Assistant);
            Assert.IsTrue(choice.Message.Content!.Equals(cumulativeDelta));
            Debug.Log(response.ToString());
            response.GetUsage();
        }

        [Test]
        public async Task Test_01_03_JsonMode()
        {
            Assert.IsNotNull(OpenAIClient.ChatEndpoint);
            var messages = new List<Message>
            {
                new Message(Role.System, "You are a helpful assistant designed to output JSON."),
                new Message(Role.User, "Who won the world series in 2020?"),
            };
            var chatRequest = new ChatRequest(messages, "gpt-4-1106-preview", responseFormat: ChatResponseFormat.Json);
            var response = await OpenAIClient.ChatEndpoint.GetCompletionAsync(chatRequest);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Choices);
            Assert.IsNotEmpty(response.Choices);

            foreach (var choice in response.Choices)
            {
                Debug.Log($"[{choice.Index}] {choice.Message.Role}: {choice} | Finish Reason: {choice.FinishReason}");
            }

            response.GetUsage();
        }

        [Test]
        public async Task Test_02_01_GetChatToolCompletion()
        {
            Assert.IsNotNull(OpenAIClient.ChatEndpoint);

            var messages = new List<Message>
            {
                new Message(Role.System, "You are a helpful weather assistant."),
                new Message(Role.User, "What's the weather like today?"),
            };

            foreach (var message in messages)
            {
                Debug.Log($"{message.Role}: {message.Content}");
            }

            var tools = new List<Tool>
            {
                new Function(
                    nameof(WeatherService.GetCurrentWeather),
                    "Get the current weather in a given location",
                     new JObject
                     {
                         ["type"] = "object",
                         ["properties"] = new JObject
                         {
                             ["location"] = new JObject
                             {
                                 ["type"] = "string",
                                 ["description"] = "The city and state, e.g. San Francisco, CA"
                             },
                             ["unit"] = new JObject
                             {
                                 ["type"] = "string",
                                 ["enum"] = new JArray {"celsius", "fahrenheit"}
                             }
                         },
                         ["required"] = new JArray { "location", "unit" }
                     })
            };
            var chatRequest = new ChatRequest(messages, tools: tools, toolChoice: "auto");
            var response = await OpenAIClient.ChatEndpoint.GetCompletionAsync(chatRequest);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Choices);
            Assert.IsTrue(response.Choices.Count == 1);
            messages.Add(response.FirstChoice.Message);

            Debug.Log($"{response.FirstChoice.Message.Role}: {response.FirstChoice} | Finish Reason: {response.FirstChoice.FinishReason}");

            var locationMessage = new Message(Role.User, "I'm in Glasgow, Scotland");
            messages.Add(locationMessage);
            Debug.Log($"{locationMessage.Role}: {locationMessage.Content}");
            chatRequest = new ChatRequest(messages, tools: tools, toolChoice: "auto");
            response = await OpenAIClient.ChatEndpoint.GetCompletionAsync(chatRequest);

            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Choices);
            Assert.IsTrue(response.Choices.Count == 1);
            messages.Add(response.FirstChoice.Message);

            if (!string.IsNullOrEmpty(response.ToString()))
            {
                Debug.Log($"{response.FirstChoice.Message.Role}: {response.FirstChoice} | Finish Reason: {response.FirstChoice.FinishReason}");

                var unitMessage = new Message(Role.User, "celsius");
                messages.Add(unitMessage);
                Debug.Log($"{unitMessage.Role}: {unitMessage.Content}");
                chatRequest = new ChatRequest(messages, tools: tools, toolChoice: "auto");
                response = await OpenAIClient.ChatEndpoint.GetCompletionAsync(chatRequest);
                Assert.IsNotNull(response);
                Assert.IsNotNull(response.Choices);
                Assert.IsTrue(response.Choices.Count == 1);
            }

            Assert.IsTrue(response.FirstChoice.FinishReason == "tool_calls");
            var usedTool = response.FirstChoice.Message.ToolCalls[0];
            Assert.IsNotNull(usedTool);
            Assert.IsTrue(usedTool.Function.Name == nameof(WeatherService.GetCurrentWeather));
            Debug.Log($"{response.FirstChoice.Message.Role}: {usedTool.Function.Name} | Finish Reason: {response.FirstChoice.FinishReason}");
            Debug.Log($"{usedTool.Function.Arguments}");
            var functionArgs = JsonConvert.DeserializeObject<WeatherArgs>(usedTool.Function.Arguments.ToString());
            var functionResult = WeatherService.GetCurrentWeather(functionArgs);
            Assert.IsNotNull(functionResult);
            messages.Add(new Message(usedTool, functionResult));
            Debug.Log($"{Role.Tool}: {functionResult}");
            chatRequest = new ChatRequest(messages, tools: tools, toolChoice: "auto");
            response = await OpenAIClient.ChatEndpoint.GetCompletionAsync(chatRequest);
            Debug.Log(response);
        }

        [Test]
        public async Task Test_02_02_GetChatToolCompletion_Streaming()
        {
            Assert.IsNotNull(OpenAIClient.ChatEndpoint);
            var messages = new List<Message>
            {
                new Message(Role.System, "You are a helpful weather assistant."),
                new Message(Role.User, "What's the weather like today?"),
            };

            foreach (var message in messages)
            {
                Debug.Log($"{message.Role}: {message.Content}");
            }

            var tools = new List<Tool>
            {
                new Function(
                    nameof(WeatherService.GetCurrentWeather),
                    "Get the current weather in a given location",
                    new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject
                        {
                            ["location"] = new JObject
                            {
                                ["type"] = "string",
                                ["description"] = "The city and state, e.g. San Francisco, CA"
                            },
                            ["unit"] = new JObject
                            {
                                ["type"] = "string",
                                ["enum"] = new JArray {"celsius", "fahrenheit"}
                            }
                        },
                        ["required"] = new JArray { "location", "unit" }
                    })
            };
            var chatRequest = new ChatRequest(messages, tools: tools, toolChoice: "auto");
            var response = await OpenAIClient.ChatEndpoint.StreamCompletionAsync(chatRequest, partialResponse =>
            {
                Assert.IsNotNull(partialResponse);
                Assert.NotNull(partialResponse.Choices);
                Assert.NotZero(partialResponse.Choices.Count);
            });
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Choices);
            Assert.IsTrue(response.Choices.Count == 1);
            messages.Add(response.FirstChoice.Message);

            var locationMessage = new Message(Role.User, "I'm in Glasgow, Scotland");
            messages.Add(locationMessage);
            Debug.Log($"{locationMessage.Role}: {locationMessage.Content}");
            chatRequest = new ChatRequest(messages, tools: tools, toolChoice: "auto");
            response = await OpenAIClient.ChatEndpoint.StreamCompletionAsync(chatRequest, partialResponse =>
            {
                Assert.IsNotNull(partialResponse);
                Assert.NotNull(partialResponse.Choices);
                Assert.NotZero(partialResponse.Choices.Count);
            });
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Choices);
            Assert.IsTrue(response.Choices.Count == 1);
            messages.Add(response.FirstChoice.Message);

            if (!string.IsNullOrEmpty(response.ToString()))
            {
                Debug.Log($"{response.FirstChoice.Message.Role}: {response.FirstChoice} | Finish Reason: {response.FirstChoice.FinishReason}");

                var unitMessage = new Message(Role.User, "celsius");
                messages.Add(unitMessage);
                Debug.Log($"{unitMessage.Role}: {unitMessage.Content}");
                chatRequest = new ChatRequest(messages, tools: tools, toolChoice: "auto");
                response = await OpenAIClient.ChatEndpoint.StreamCompletionAsync(chatRequest, partialResponse =>
                {
                    Assert.IsNotNull(partialResponse);
                    Assert.NotNull(partialResponse.Choices);
                    Assert.NotZero(partialResponse.Choices.Count);
                });
                Assert.IsNotNull(response);
                Assert.IsNotNull(response.Choices);
                Assert.IsTrue(response.Choices.Count == 1);
            }

            Assert.IsTrue(response.FirstChoice.FinishReason == "tool_calls");
            var usedTool = response.FirstChoice.Message.ToolCalls[0];
            Assert.IsNotNull(usedTool);
            Assert.IsTrue(usedTool.Function.Name == nameof(WeatherService.GetCurrentWeather));
            Debug.Log($"{response.FirstChoice.Message.Role}: {usedTool.Function.Name} | Finish Reason: {response.FirstChoice.FinishReason}");
            Debug.Log($"{usedTool.Function.Arguments}");

            var functionArgs = JsonConvert.DeserializeObject<WeatherArgs>(usedTool.Function.Arguments.ToString());
            var functionResult = WeatherService.GetCurrentWeather(functionArgs);
            Assert.IsNotNull(functionResult);
            messages.Add(new Message(usedTool, functionResult));
            Debug.Log($"{Role.Tool}: {functionResult}");

            chatRequest = new ChatRequest(messages, tools: tools, toolChoice: "auto");
            response = await OpenAIClient.ChatEndpoint.StreamCompletionAsync(chatRequest, partialResponse =>
            {
                Assert.IsNotNull(partialResponse);
                Assert.NotNull(partialResponse.Choices);
                Assert.NotZero(partialResponse.Choices.Count);
            });
            Assert.IsNotNull(response);
        }

        [Test]
        public async Task Test_02_03_ChatCompletion_Multiple_Tools_Streaming()
        {
            Assert.IsNotNull(OpenAIClient.ChatEndpoint);
            var messages = new List<Message>
            {
                new Message(Role.System, "You are a helpful weather assistant."),
                new Message(Role.User, "What's the weather like today in San Diego and LA?"),
            };

            var tools = new List<Tool>
            {
                new Function(
                    nameof(WeatherService.GetCurrentWeather),
                    "Get the current weather in a given location",
                    new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject
                        {
                            ["location"] = new JObject
                            {
                                ["type"] = "string",
                                ["description"] = "The city and state, e.g. San Francisco, CA"
                            },
                            ["unit"] = new JObject
                            {
                                ["type"] = "string",
                                ["enum"] = new JArray { "celsius", "fahrenheit" }
                            }
                        },
                        ["required"] = new JArray { "location", "unit" }
                    })
            };

            var chatRequest = new ChatRequest(messages, model: "gpt-4-1106-preview", tools: tools, toolChoice: "auto");
            var response = await OpenAIClient.ChatEndpoint.StreamCompletionAsync(chatRequest, partialResponse =>
            {
                Assert.IsNotNull(partialResponse);
                Assert.NotNull(partialResponse.Choices);
                Assert.NotZero(partialResponse.Choices.Count);
            });

            messages.Add(response.FirstChoice.Message);

            var toolCalls = response.FirstChoice.Message.ToolCalls;

            Assert.NotNull(toolCalls);
            Assert.AreEqual(2, toolCalls.Count);

            foreach (var toolCall in toolCalls)
            {
                messages.Add(new Message(toolCall, "Sunny!"));
            }

            chatRequest = new ChatRequest(messages, model: "gpt-4-1106-preview", tools: tools, toolChoice: "auto");
            response = await OpenAIClient.ChatEndpoint.GetCompletionAsync(chatRequest);

            Assert.IsNotNull(response);
        }

        [Test]
        public async Task Test_02_04_GetChatToolForceCompletion()
        {
            Assert.IsNotNull(OpenAIClient.ChatEndpoint);
            var messages = new List<Message>
            {
                new Message(Role.System, "You are a helpful weather assistant."),
                new Message(Role.User, "What's the weather like today?"),
            };

            foreach (var message in messages)
            {
                Debug.Log($"{message.Role}: {message.Content}");
            }

            var tools = new List<Tool>
            {
                new Function(
                    nameof(WeatherService.GetCurrentWeather),
                    "Get the current weather in a given location",
                     new JObject
                     {
                         ["type"] = "object",
                         ["properties"] = new JObject
                         {
                             ["location"] = new JObject
                             {
                                 ["type"] = "string",
                                 ["description"] = "The city and state, e.g. San Francisco, CA"
                             },
                             ["unit"] = new JObject
                             {
                                 ["type"] = "string",
                                 ["enum"] = new JArray {"celsius", "fahrenheit"}
                             }
                         },
                         ["required"] = new JArray { "location", "unit" }
                     })
            };
            var chatRequest = new ChatRequest(messages, tools: tools);
            var response = await OpenAIClient.ChatEndpoint.GetCompletionAsync(chatRequest);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Choices);
            Assert.IsTrue(response.Choices.Count == 1);
            messages.Add(response.FirstChoice.Message);

            Debug.Log($"{response.FirstChoice.Message.Role}: {response.FirstChoice} | Finish Reason: {response.FirstChoice.FinishReason}");

            var locationMessage = new Message(Role.User, "I'm in Glasgow, Scotland");
            messages.Add(locationMessage);
            Debug.Log($"{locationMessage.Role}: {locationMessage.Content}");
            chatRequest = new ChatRequest(
                messages,
                tools: tools,
                toolChoice: nameof(WeatherService.GetCurrentWeather));
            response = await OpenAIClient.ChatEndpoint.GetCompletionAsync(chatRequest);

            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Choices);
            Assert.IsTrue(response.Choices.Count == 1);
            messages.Add(response.FirstChoice.Message);

            Assert.IsTrue(response.FirstChoice.FinishReason == "stop");
            var usedTool = response.FirstChoice.Message.ToolCalls[0];
            Assert.IsNotNull(usedTool);
            Assert.IsTrue(usedTool.Function.Name == nameof(WeatherService.GetCurrentWeather));
            Debug.Log($"{response.FirstChoice.Message.Role}: {usedTool.Function.Name} | Finish Reason: {response.FirstChoice.FinishReason}");
            Debug.Log($"{usedTool.Function.Arguments}");
            var functionArgs = JsonConvert.DeserializeObject<WeatherArgs>(usedTool.Function.Arguments.ToString());
            var functionResult = WeatherService.GetCurrentWeather(functionArgs);
            Assert.IsNotNull(functionResult);
            messages.Add(new Message(usedTool, functionResult));
            Debug.Log($"{Role.Tool}: {functionResult}");
        }

        [Test]
        public async Task Test_03_01_GetChatVision()
        {
            Assert.IsNotNull(OpenAIClient.ChatEndpoint);
            var messages = new List<Message>
            {
                new Message(Role.System, "You are a helpful assistant."),
                new Message(Role.User, new List<Content>
                {
                    "What's in this image?",
                    new ImageUrl("https://upload.wikimedia.org/wikipedia/commons/thumb/d/dd/Gfp-wisconsin-madison-the-nature-boardwalk.jpg/2560px-Gfp-wisconsin-madison-the-nature-boardwalk.jpg", ImageDetail.Low)
                })
            };
            var chatRequest = new ChatRequest(messages, model: "gpt-4-vision-preview");
            var response = await OpenAIClient.ChatEndpoint.GetCompletionAsync(chatRequest);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Choices);
            Debug.Log($"{response.FirstChoice.Message.Role}: {response.FirstChoice} | Finish Reason: {response.FirstChoice.FinishDetails}");
            response.GetUsage();
        }

        [Test]
        public async Task Test_03_02_GetChatVisionStreaming()
        {
            Assert.IsNotNull(OpenAIClient.ChatEndpoint);
            var messages = new List<Message>
            {
                new Message(Role.System, "You are a helpful assistant."),
                new Message(Role.User, new List<Content>
                {
                    "What's in this image?",
                    new ImageUrl("https://upload.wikimedia.org/wikipedia/commons/thumb/d/dd/Gfp-wisconsin-madison-the-nature-boardwalk.jpg/2560px-Gfp-wisconsin-madison-the-nature-boardwalk.jpg", ImageDetail.Low)
                })
            };
            var chatRequest = new ChatRequest(messages, model: "gpt-4-vision-preview");
            var response = await OpenAIClient.ChatEndpoint.StreamCompletionAsync(chatRequest, partialResponse =>
            {
                Assert.IsNotNull(partialResponse);
                Assert.NotNull(partialResponse.Choices);
                Assert.NotZero(partialResponse.Choices.Count);
            });
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Choices);
            Debug.Log($"{response.FirstChoice.Message.Role}: {response.FirstChoice} | Finish Reason: {response.FirstChoice.FinishDetails}");
            response.GetUsage();
        }

        [Test]
        public async Task Test_03_03_GetChatVision_Texture()
        {
            Assert.IsNotNull(OpenAIClient.ChatEndpoint);
            var imageAssetPath = AssetDatabase.GUIDToAssetPath("230fd778637d3d84d81355c8c13b1999");
            var image = AssetDatabase.LoadAssetAtPath<Texture2D>(imageAssetPath);
            var messages = new List<Message>
            {
                new Message(Role.System, "You are a helpful assistant."),
                new Message(Role.User, new List<Content>
                {
                    "What's in this image?",
                    image
                })
            };
            var chatRequest = new ChatRequest(messages, model: "gpt-4-vision-preview");
            var response = await OpenAIClient.ChatEndpoint.GetCompletionAsync(chatRequest);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Choices);
            Debug.Log($"{response.FirstChoice.Message.Role}: {response.FirstChoice} | Finish Reason: {response.FirstChoice.FinishDetails}");
            response.GetUsage();
        }
    }
}
