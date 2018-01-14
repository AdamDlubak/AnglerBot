using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Flurl.Http;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using Newtonsoft.Json;
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
      var activity = context.Activity as Activity;
      if (activity == null)
        return;

      var attachment = message.Attachments.FirstOrDefault();
      if (attachment != null && attachment.ContentType.Contains("image"))
      {
        var speciesJsonString = await GetSpecies(attachment.ContentUrl);
        var speciesResult = JsonConvert.DeserializeObject<ImageResultContent>(speciesJsonString);
        var speciesTag =
          speciesResult.Predictions.FirstOrDefault(
            c => Math.Abs(c.ProbabilityValue - speciesResult.Predictions.Max(e => e.ProbabilityValue)) < 0.000001)?.Tag;

        var response = await new QnAServiceRequestPerformer().SendRequest<QuestionResponse>($"Wymiar ochronny {speciesTag}");
        var part1 = response.Answers.FirstOrDefault()?.Answer;

        response = await new QnAServiceRequestPerformer().SendRequest<QuestionResponse>($"Okres ochronny {speciesTag}");
        var part2 = response.Answers.FirstOrDefault()?.Answer;

        var reply = activity.CreateReply($"{part1}. Okres ochronny dla tego gatunku to: {part2}");
        await new ConnectorClient(new Uri(activity.ServiceUrl)).Conversations.ReplyToActivityAsync(reply);
      }
      else
      {
        await context.Forward(new BasicQnAMakerDialog(), AfterAnswerAsync, message, CancellationToken.None);
      }
    }

    private async Task AfterAnswerAsync(IDialogContext context, IAwaitable<IMessageActivity> result)
    {
      // wait for the next user message
      context.Wait(MessageReceivedAsync);
    }

    private static byte[] GetImageAsByteArray(string imageUrl)
    {
      var webClient = new WebClient();
      return webClient.DownloadData(imageUrl);
    }

    private async Task<string> GetSpecies(string imageUrl)
    {
      var client = new HttpClient();

      // Request headers - replace this example key with your valid subscription key.
      client.DefaultRequestHeaders.Add("Prediction-Key", "2884ad7130784b71bddc4ae01ef5c096");

      // Prediction URL - replace this example URL with your valid prediction URL.
      var url = "https://southcentralus.api.cognitive.microsoft.com/customvision/v1.1/Prediction/efc9c59a-9113-4973-882f-683a9e1a5d7f/image?iterationId=5280d7ab-1467-484f-8058-85ca46de929e";

      // Request body. Try this sample with a locally stored image.
      byte[] byteData = GetImageAsByteArray(imageUrl);

      using (var content = new ByteArrayContent(byteData))
      {
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        var response = await client.PostAsync(url, content);
        return await response.Content.ReadAsStringAsync();
      }
    }
  }

  // For more information about this template visit http://aka.ms/azurebots-csharp-qnamaker
  [Serializable]
  [QnAMakerService("2271ce4c39c3491883941f69d1ecfc90", "73015880-cac0-400a-af2b-fea56e55e406")]
  public class BasicQnAMakerDialog : QnAMakerDialog<IMessageActivity>
  {
    public override async Task NoMatchHandler(IDialogContext context, string originalQueryText)
    {
      var activity = context.Activity as Activity;
      if (activity == null)
        return;

      var reply = activity.CreateReply($"Niestety nie znaleüliúmy øadnych informacji na ten temat...");
      await new ConnectorClient(new Uri(activity.ServiceUrl)).Conversations.ReplyToActivityAsync(reply);
    }

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
        await base.DefaultMatchHandler(context, originalQueryText, result);
      }
    }
  }

  public class QnAServiceRequestPerformer
  {
    public async Task<T> SendRequest<T>(string question)
    {
      var url = @"https://westus.api.cognitive.microsoft.com/qnamaker/v2.0/knowledgebases/73015880-cac0-400a-af2b-fea56e55e406/generateAnswer";
     return await url.WithHeader("Ocp-Apim-Subscription-Key", "2271ce4c39c3491883941f69d1ecfc90")
        .PostJsonAsync(new { question }).ReceiveJson<T>();
    }
  }

  public class ImageResultContent
  {
    [JsonProperty("Predictions")]
    public List<ImageResultPrediction> Predictions { get; set; }
  }

  public class ImageResultPrediction
  {
    [JsonProperty("Probability")]
    public string Probability { get; set; }

    public double ProbabilityValue => Convert.ToDouble(Probability);

    [JsonProperty("Tag")]
    public string Tag { get; set; }
  }

  public class RequestQuestion
  {
    [JsonProperty("question")]
    public string Question { get; set; }
  }

  public class QuestionResponse
  {
    [JsonProperty("answers")]
    public List<QuestionResponseNode> Answers { get; set; }
  }

  public class QuestionResponseNode
  {
    [JsonProperty("answer")]
    public string Answer { get; set; }
  }
}