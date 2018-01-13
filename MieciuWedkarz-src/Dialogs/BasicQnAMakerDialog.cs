using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using QnAMakerDialog;

namespace Microsoft.Bot.Sample.QnABot
{
  [Serializable]
  public class RootDialog : IDialog<object>
  {
    public async Task StartAsync(IDialogContext context)
    {
      /* Wait until the first message is received from the conversation and call MessageReceviedAsync 
      *  to process that message. */
      context.Wait(MessageReceivedAsync);
    }

    private async Task MessageReceivedAsync(IDialogContext context, IAwaitable<IMessageActivity> result)
    {
      var message = await result;
      await context.Forward(new BasicQnAMakerDialog(), AfterAnswerAsync, message, CancellationToken.None);
    }

    private async Task AfterAnswerAsync(IDialogContext context, IAwaitable<IMessageActivity> result)
    {
      // wait for the next user message
      context.Wait(MessageReceivedAsync);
    }
  }

  // For more information about this template visit http://aka.ms/azurebots-csharp-qnamaker
  [Serializable]
  [QnAMakerService("2271ce4c39c3491883941f69d1ecfc90", "73015880-cac0-400a-af2b-fea56e55e406")]
  public class BasicQnAMakerDialog : QnAMakerDialog<IMessageActivity>
  {
    public override async Task DefaultMatchHandler(IDialogContext context, string originalQueryText, QnAMakerResult result)
    {
      if (result.Answer.StartsWith("http"))
      {
        var activity = context.Activity as Activity;
        if (activity == null)
          return;

        var reply = activity.CreateReply("");
        reply.Attachments = new List<Attachment>
        {
          new Attachment
          {
            ContentUrl = result.Answer,
            ContentType = "image/png",
            Name = "Bender_Rodriguez.png"
          }
        };

        await new ConnectorClient(new Uri(activity.ServiceUrl)).Conversations.ReplyToActivityAsync(reply);
      }
      else
      {
        await Task.FromResult(base.DefaultMatchHandler(context, originalQueryText, result));
      }
    }
  }
}