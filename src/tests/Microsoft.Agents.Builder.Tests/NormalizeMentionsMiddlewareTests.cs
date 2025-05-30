﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.Testing;
using Microsoft.Agents.Core.Models;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Agents.Builder.Compat;
using Xunit;

namespace Microsoft.Agents.Builder.Tests
{
    public class NormalizeMentionsMiddlewareTests
    {
        private static Regex regex = new Regex(@"(?:^|[^a-zA-Z0-9_＠!@#$%&*])(?:(?:@|＠)(?!\/))([a-zA-Z0-9/_]{1,15})(?:\b(?!@|＠)|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        [Fact]
        public async Task NormalizeMentionsVariations()
        {
            var adapter = new TestAdapter(TestAdapter.CreateConversation(nameof(NormalizeMentionsVariations)))
                .Use(new NormalizeMentionsMiddleware());

            await new TestFlow(adapter, async (context, cancellationToken) =>
            {
                Assert.False(context.Activity.Text.Contains("<at") && context.Activity.Text.Contains("</at>"));
                if (context.Activity.Entities != null)
                {
                    int i = 1;
                    foreach (Mention entity in context.Activity.Entities)
                    {
                        Assert.DoesNotContain("<at", entity.Text);
                        Assert.DoesNotContain("</at>", entity.Text);
                        Assert.Contains(entity.Text, context.Activity.Text);
                        Assert.Equal($"user{i++}", entity.Mentioned.Id);
                    }
                }

                await context.SendActivityAsync("OK");
            })
                .Send(CreateMentionActivity("this is <at>Tom</at>", CreateEntity("<at>Tom</at>", "user1")))
                    .AssertReply("OK")
                .Send(CreateMentionActivity("this is <at id='123123'>Tom</at> asdfasdf", CreateEntity("<at id='123123'>Tom</at>", "user1")))
                    .AssertReply("OK")
                .Send(CreateMentionActivity("<at>Tom</at>", CreateEntity("<at>Tom</at>", "user1")))
                    .AssertReply("OK")
                .Send(CreateMentionActivity("<at>Tom</at>test", CreateEntity("<at>Tom</at>test", "user1")))
                    .AssertReply("OK")
                .Send(CreateMentionActivity("<at>Tomtest"))
                    .AssertReply("OK")
                .Send(CreateMentionActivity("at>Tomtest</at>"))
                    .AssertReply("OK")
                .Send(CreateMentionActivity("<at>Tom</at><at>John</at>", CreateEntity("<at>Tom</at>", "user1"), CreateEntity("<at>John</at>", "user2"), CreateEntity("", "user3")))
                    .AssertReply("OK")
                .StartTestAsync();
        }

        [Fact]
        public async Task StripRecipientMentionsAndEntities()
        {
            var adapter = new TestAdapter(TestAdapter.CreateConversation(nameof(NormalizeMentionsVariations)))
                .Use(new NormalizeMentionsMiddleware());

            await new TestFlow(adapter, async (context, cancellationToken) =>
            {
                Assert.False(context.Activity.Text.Contains("<at") && context.Activity.Text.Contains("</at>"));
                Assert.DoesNotContain("bot", context.Activity.Text);
                Assert.True(context.Activity.Entities == null || context.Activity.Entities.Count == 0);
                await context.SendActivityAsync("OK");
            })
                .Send(CreateMentionActivity("this is <at>Bot</at>", CreateEntity("<at>Bot</at>", "bot")))
                    .AssertReply("OK")
                .StartTestAsync();
        }

        [Fact]
        public async Task DoesNotStripRecipientMentionsAndEntities()
        {
            var adapter = new TestAdapter(TestAdapter.CreateConversation(nameof(NormalizeMentionsVariations)))
                .Use(new NormalizeMentionsMiddleware() { RemoveRecipientMention = false });

            await new TestFlow(adapter, async (context, cancellationToken) =>
            {
                Assert.False(context.Activity.Text.Contains("<at") && context.Activity.Text.Contains("</at>"));
                Assert.Contains("Bot", context.Activity.Text);
                Assert.NotNull(context.Activity.Entities);
                var entity = context.Activity.Entities.Single() as Mention;
                Assert.DoesNotContain("<at", entity.Text);
                Assert.DoesNotContain("</at>", entity.Text);
                Assert.Contains(entity.Text, context.Activity.Text);
                Assert.Equal($"bot", entity.Mentioned.Id);
                await context.SendActivityAsync("OK");
            })
                .Send(CreateMentionActivity("this is <at>Bot</at>", CreateEntity("<at>Bot</at>", "bot")))
                    .AssertReply("OK")
                .StartTestAsync();
        }

        public Activity CreateMentionActivity(string text, params Entity[] entities)
        {
            Activity activity = new Activity();
            activity.Text = text;
            activity.Entities = entities.ToList();
            return activity;
        }

        public Entity CreateEntity(string atText, string userId)
        {
            var entity = new Mention()
            {
                Type = "mention",
                Text = atText,
                Mentioned = new ChannelAccount()
                {
                    Id = userId,
                    Name = "User",
                }
            };
            
            return entity;
        }
    }
}
