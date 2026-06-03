// AIChatBotCommand.cs
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive (RICS).
// 
// CAP Chat Interactive is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as published
// by the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

// AIChatBotCommand.cs
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive (RICS).

using RimWorld;
using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace CAP_ChatInteractive.Commands.AICommands
{
    /// <summary>
    /// Command for AI Chatbot integration (Masie Lamia / external bots).
    /// Sends recent chat history + game state to local Python bot and returns response.
    /// </summary>
    public class AIChatBotCommand : ChatCommand
    {
        public override string Name => "ricsaichatbot";
        public override string Alias => ""; // Users customize via settings alias

        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(8) // Reasonable timeout
        };

        public override bool CanExecute(ChatMessageWrapper message)
        {
            return base.CanExecute(message);
        }

        public override string Execute(ChatMessageWrapper message, string[] args)
        {
            var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
            if (settings == null || !settings.AIChatBotActive)
            {
                return "The AI storyteller is currently sleeping... Enable AIChatBotActive in RICS settings to wake them up!";
            }

            if (!ChatCommandProcessor.IsGameReady())
            {
                return "Please wait until the colony is fully loaded before chatting with the AI.";
            }

            string userInput = string.Join(" ", args).Trim();
            if (string.IsNullOrWhiteSpace(userInput))
            {
                return "What would you like to talk about?";
            }

            try
            {
                // Get data for the bot
                var recentChat = ChatMessageLogger.GetRecentMessagesForAI(35);
                var gameState = GetGameStateForAI();

                // Build payload
                var payload = new
                {
                    username = message.Username,
                    input = userInput,
                    gameState = gameState,
                    recentChat = recentChat.Select(m => new
                    {
                        username = m.Username,
                        text = m.Text,
                        platform = m.Platform,
                        isFromRICS = m.IsFromRICS,
                        timestamp = m.Timestamp
                    }).ToList()
                };

                string jsonPayload = Newtonsoft.Json.JsonConvert.SerializeObject(payload);

                // Send to Python (or any other) bot
                var response = SendToAIBotAsync(jsonPayload).GetAwaiter().GetResult();

                if (!string.IsNullOrWhiteSpace(response))
                {
                    // Log the bot's reply using generic "AI" tag so other bots work cleanly
                    string botName = settings.AIChatBotName ?? "AI";
                    ChatMessageLogger.AddMessage(botName, response, "AI", isFromRICS: true);
                    return response;
                }

                return "The AI is thinking... (No response received)";
            }
            catch (Exception ex)
            {
                Logger.Error($"[AI ChatBot Command] Error: {ex.Message}");
                return "Something went wrong while talking to the AI. Please try again later.";
            }
        }

        private object GetGameStateForAI()
        {
            var service = Current.Game?.GetComponent<CAPChatInteractive_GameComponent>()?._aiChatBotService;
            if (service != null)
            {
                string json = service.GetGameStateJson();
                return Newtonsoft.Json.JsonConvert.DeserializeObject(json); // Convert back to object for payload
            }

            // Fallback
            return new { status = "unavailable" };
        }

        private async Task<string> SendToAIBotAsync(string jsonPayload)
        {
            var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
            if (settings == null) return null;

            try
            {
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(settings.AIChatBotEndpoint.TrimEnd('/') + "/chat", content);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync();
                }
                else
                {
                    Logger.Warning($"AI Bot returned status: {response.StatusCode}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to contact AI bot: {ex.Message}");
                return null;
            }
        }
    }
}