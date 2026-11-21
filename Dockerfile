FROM python:3.11-slim

WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends \
    gcc \
    curl \
    && rm -rf /var/lib/apt/lists/*

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY . .

EXPOSE 8080 8001 8002 8003 8004 50051

CMD ["uvicorn", "src.gateway.main:app", "--host", "0.0.0.0", "--port", "8080"]
