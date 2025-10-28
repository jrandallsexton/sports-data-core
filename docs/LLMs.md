# 🧠 LLM Integration Strategy for SportDeets

## 1. Self-Hosted LLMs vs. Azure AI Services

| Option                                  | Pros                                                                 | Cons                                                                 |
|-----------------------------------------|----------------------------------------------------------------------|----------------------------------------------------------------------|
| **Ollama on Azure App Services or ACA** | - Lower long-term cost if usage grows<br>- Full control over model<br>- Easier integration with your stack | - Requires GPU-enabled plans (limited)<br>- More DevOps effort<br>- Some models require persistent disk |
| **Ollama in Azure Container Apps (CPU)**| - Cheaper than GPU App Services<br>- Flexible with quantized models | - Slower inference (unless using small models) |
| **Azure AI Services (OpenAI)**          | - Fully managed<br>- Scales easily<br>- Tight Azure integration      | - Expensive per-token<br>- Limited fine-tuning unless paying extra |

**Initial Recommendation**:  
Use **Ollama in Azure Container Apps (ACA)** with quantized 3B or 7B models (e.g., LLaMA2, Mistral). Scale later with GPU-backed services or swap to Azure OpenAI if needed.

---

## 2. Training and Data Ingestion

- Canonical data from `Producer` is the **source of truth**.
- Introduce a service (e.g., `LLMDataPreparer`) to:
  - Export structured JSONL training sets.
  - Provide data snapshots (e.g., current season, completed games).
  - Deliver metadata (teams, players, matchups) for embeddings.

### Suggested Flow:
1. Periodic export to Azure Blob or Data Lake.
2. Optional: Secure internal API (e.g., `/api/train-feed`) for structured real-time training data access.
3. Trigger batch training pipelines (local or cloud-based) when new exports are detected.

---

## 3. Predictions, Feedback, and Re-Training

### Storage of Model Outputs
Store LLM outputs in a dedicated SQL or document table with:

- Input hash (to deduplicate)
- Timestamp
- Model version
- Prediction result
- Confidence (if available)

### Post-Game Evaluation
- Job compares predictions to real outcomes.
- Annotates prediction record with correctness.
- Aggregates performance metrics (accuracy, precision, recall).

### Retraining Strategy
- When error rate or data freshness threshold is exceeded:
  - Generate new JSONL from `Producer`.
  - Launch fine-tuning job (self-hosted or via OpenAI/Azure AI).
  - Version and tag the resulting model.

---

## Notes
- **Hot data** (current season) should stay in fast-access DB (e.g., Cosmos DB).
- **Cold data** (historical JSON) may go to Data Lake for archive + training prep.
- Consider logging inference events to Seq or Log Analytics for debugging and trend analysis.

