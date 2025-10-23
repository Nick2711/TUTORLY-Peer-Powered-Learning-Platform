from fastapi import FastAPI
from pydantic import BaseModel
from sentence_transformers import SentenceTransformer
import uvicorn
import logging

# Configure logging
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

app = FastAPI()

# Load model with error handling
try:
    model = SentenceTransformer("all-MiniLM-L6-v2")
    logger.info("Model loaded successfully")
except Exception as e:
    logger.error(f"Failed to load model: {e}")
    model = None

class TextRequest(BaseModel):
    text: str

@app.get("/health")
def health_check():
    """Health check endpoint"""
    if model is None:
        return {"status": "error", "message": "Model not loaded"}
    return {"status": "healthy", "message": "Embedding API is running"}

@app.post("/embed")
def embed_text(req: TextRequest):
    if model is None:
        return {"error": "Model not loaded"}
    
    try:
        embedding = model.encode(req.text).tolist()
        return {"embedding": embedding}
    except Exception as e:
        logger.error(f"Error generating embedding: {e}")
        return {"error": str(e)}

if __name__ == "__main__":
    uvicorn.run(app, host="0.0.0.0", port=8000, log_level="info")
