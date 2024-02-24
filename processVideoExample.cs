/*
  Example usage of Azure Cognitive AI Service
  This method will consume a language model by passing text from a YouTube description to a Named Entity Recognition Model.
  The model is trained on a corpus of data looking for specific keywords. 
  The API returns a Categorized Entity and Confidence Score. If confidence score is high enough, save the entity
  for further aggregation.
  
  See:
  https://github.com/Azure/azure-sdk-for-net/blob/Azure.AI.TextAnalytics_5.3.0/sdk/textanalytics/Azure.AI.TextAnalytics/README.md
  https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/textanalytics/Azure.AI.TextAnalytics/samples/Sample8_RecognizeCustomEntities.md
*/

private static async Task processRawDescritpionNERAI(Models.Video video, ILogger log){

  try {
  
    log.LogInformation($"Start processing AI for videoID: {video.VideoId}");

    string endpoint = Environment.GetEnvironmentVariable("language_ai_endpoint");
    string apiKey = Environment.GetEnvironmentVariable("language_ai_api_key");
    var client = new TextAnalyticsClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
    var batchDocuments = new List<TextDocumentInput>();

    if(string.IsNullOrEmpty(video.DescriptionRaw)) {
        return;
    }

    batchDocuments.Add(
        new TextDocumentInput("1", video.DescriptionRaw)
        {
            Language = "en",
        });

    string projectName = Environment.GetEnvironmentVariable("language_ai_project_name");
    string deploymentName = Environment.GetEnvironmentVariable("language_ai_deployment_name");
    var actions = new TextAnalyticsActions()
    {
        RecognizeCustomEntitiesActions = new List<RecognizeCustomEntitiesAction>()
        {
            new RecognizeCustomEntitiesAction(projectName, deploymentName)
        }
    };

    AnalyzeActionsOperation operation = await client.StartAnalyzeActionsAsync(batchDocuments, actions);

    await operation.WaitForCompletionAsync();

    await foreach (AnalyzeActionsResult documentsInPage in operation.Value)
    {
        IReadOnlyCollection<RecognizeCustomEntitiesActionResult> customEntitiesActionResults = documentsInPage.RecognizeCustomEntitiesResults;
        foreach (RecognizeCustomEntitiesActionResult customEntitiesActionResult in customEntitiesActionResults)
        {
            foreach (RecognizeEntitiesResult documentResults in customEntitiesActionResult.DocumentsResults)
            {
                var processedText = new StringBuilder();

                foreach (CategorizedEntity entity in documentResults.Entities)
                {
                    var ner = new NER(){
                        Text = entity.Text,
                        Category = (string)entity.Category,
                        ConfidenceScore = entity.ConfidenceScore,
                        Length = entity.Length,
                        Offset = entity.Offset,
                        SubCategory = entity.SubCategory
                    };

                    video.CategorizedEntities.Add(ner);

                    if(entity.ConfidenceScore >= .8) {
                        processedText.Append(entity.Text);
                        processedText.AppendLine();
                    }

                }

                video.ProcessedText = processedText.ToString();
            }
        }
    }

    log.LogInformation($"Completed processing AI for videoID: {video.VideoId}");
  
    } catch(Exception ex){
        log.LogError($"Error Processing AI for videoID: {video.VideoId}");
        log.LogError($"Message: {ex.Message}");
    }
}
