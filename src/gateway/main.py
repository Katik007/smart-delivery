from fastapi import FastAPI, Request, Response
from fastapi.responses import HTMLResponse, FileResponse
from fastapi.staticfiles import StaticFiles
import httpx
import os
from datetime import datetime

app = FastAPI(title="SmartDelivery ApiGateway", version="1.0.0")

AUTH_URL = os.getenv("AUTH_SERVICE_URL", "http://localhost:8001")
CATALOG_URL = os.getenv("CATALOG_SERVICE_URL", "http://localhost:8002")
ORDER_URL = os.getenv("ORDER_SERVICE_URL", "http://localhost:8003")
DELIVERY_URL = os.getenv("DELIVERY_SERVICE_URL", "http://localhost:8004")

client = httpx.AsyncClient(timeout=10.0)


@app.on_event("shutdown")
async def shutdown_event():
    await client.aclose()


@app.get("/health")
async def health():
    return {
        "status": "Healthy",
        "gateway": "SmartDelivery.ApiGateway",
        "timestamp": datetime.utcnow().isoformat()
    }


# Reverse Proxy routing function
async def proxy_request(target_base: str, path: str, request: Request) -> Response:
    url = f"{target_base}{path}"
    if request.url.query:
        url += f"?{request.url.query}"

    headers = dict(request.headers)
    headers.pop("host", None)

    content = await request.body()

    try:
        resp = await client.request(
            method=request.method,
            url=url,
            headers=headers,
            content=content
        )
        return Response(content=resp.content, status_code=resp.status_code, headers=dict(resp.headers))
    except Exception as e:
        return Response(
            content=f'{{"error": "Microservice unavailable", "target": "{target_base}", "details": "{str(e)}"}}',
            status_code=503,
            media_type="application/json"
        )


@app.api_route("/api/auth/{path:path}", methods=["GET", "POST", "PUT", "DELETE"])
async def route_auth(path: str, request: Request):
    return await proxy_request(AUTH_URL, f"/api/auth/{path}", request)


@app.api_route("/api/catalog/{path:path}", methods=["GET", "POST", "PUT", "DELETE"])
async def route_catalog(path: str, request: Request):
    return await proxy_request(CATALOG_URL, f"/api/catalog/{path}", request)


@app.api_route("/api/orders/{path:path}", methods=["GET", "POST", "PUT", "DELETE"])
async def route_orders(path: str, request: Request):
    return await proxy_request(ORDER_URL, f"/api/orders/{path}", request)


@app.api_route("/api/delivery/{path:path}", methods=["GET", "POST", "PUT", "DELETE"])
async def route_delivery(path: str, request: Request):
    return await proxy_request(DELIVERY_URL, f"/api/delivery/{path}", request)


# Static dashboard
HTML_PATH = os.path.join(os.path.dirname(__file__), "static", "index.html")


@app.get("/", response_class=HTMLResponse)
async def serve_root():
    if os.path.exists(HTML_PATH):
        with open(HTML_PATH, "r", encoding="utf-8") as f:
            return HTMLResponse(content=f.read())
    return HTMLResponse("<h1>SmartDelivery API Gateway Active</h1><p>Visit /health or /api/catalog/products</p>")


@app.get("/index.html", response_class=HTMLResponse)
async def serve_index():
    return await serve_root()
