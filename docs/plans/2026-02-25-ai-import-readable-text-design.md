# AI Import Readable Text Design

**Date:** 2026-02-25
**Owner:** Codex

## Goal
Improve AI-based URL import quality by sending extracted readable text from the target webpage (up to 100 KB) in the AI prompt, and log the actual JSON payload sent to OpenAI/Anthropic.

## Non-Goals
- No full HTML passthrough to the AI.
- No external readability libraries or heavy dependencies.
- No change to JSON-LD parsing behavior.

## Architecture
- Extend `RecipeImportService` to extract readable text from fetched HTML using HtmlAgilityPack and simple heuristics (remove scripts/styles/nav/header/footer, keep visible text).
- Pass the extracted text into `AiRecipeImportService.ImportAsync` and include it in the prompt.
- Enforce a 100 KB character limit for extracted text; if truncated, note it in the prompt.
- Log the full JSON request payload sent to OpenAI/Anthropic at `Debug` level (payload includes model + messages, no API key).

## Data Flow
1. Fetch HTML from URL.
2. Extract readable text.
3. Truncate to 100 KB (if needed).
4. Build prompt with URL + readable text.
5. Send to AI and parse response.

## Error Handling
- If extraction yields too little text, fall back to URL-only prompt.
- If text is truncated, add a prompt note to improve AI interpretation.

## Logging
- Log AI prompt payload JSON at `Debug` for both providers.
- Keep existing AI response JSON logging.

## Testing
- Manual: import a recipe and verify request payload logs contain extracted text and “truncated” note when applicable.
