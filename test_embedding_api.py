#!/usr/bin/env python3
"""
Test script for the embedding API
Run this to verify the embedding API works correctly
"""

import requests
import json
import time

def test_embedding_api():
    base_url = "http://localhost:8000"
    
    print("Testing Embedding API...")
    print("=" * 50)
    
    # Test health endpoint
    try:
        print("1. Testing health endpoint...")
        response = requests.get(f"{base_url}/health", timeout=5)
        if response.status_code == 200:
            data = response.json()
            print(f"   ✅ Health check passed: {data}")
        else:
            print(f"   ❌ Health check failed: {response.status_code}")
            return False
    except requests.exceptions.RequestException as e:
        print(f"   ❌ Health check failed: {e}")
        return False
    
    # Test embedding endpoint
    try:
        print("\n2. Testing embedding endpoint...")
        test_text = "This is a test sentence for embedding generation."
        
        response = requests.post(
            f"{base_url}/embed",
            json={"text": test_text},
            timeout=10
        )
        
        if response.status_code == 200:
            data = response.json()
            if "embedding" in data:
                embedding = data["embedding"]
                print(f"   ✅ Embedding generated successfully")
                print(f"   📊 Embedding dimension: {len(embedding)}")
                print(f"   📊 First 5 values: {embedding[:5]}")
            else:
                print(f"   ❌ Invalid response format: {data}")
                return False
        else:
            print(f"   ❌ Embedding request failed: {response.status_code}")
            print(f"   Response: {response.text}")
            return False
            
    except requests.exceptions.RequestException as e:
        print(f"   ❌ Embedding request failed: {e}")
        return False
    
    print("\n" + "=" * 50)
    print("✅ All tests passed! Embedding API is working correctly.")
    return True

if __name__ == "__main__":
    # Wait a moment for the API to start if it's starting up
    print("Waiting for API to be ready...")
    time.sleep(2)
    
    success = test_embedding_api()
    if not success:
        print("\n❌ Tests failed. Make sure the embedding API is running on port 8000.")
        exit(1)
