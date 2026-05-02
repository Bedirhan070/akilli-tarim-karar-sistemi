# Akilli Tarim Asistani - ML Servisi

Dogu ve Kuzeydogu Anadolu bugday tarimi icin ML servisi.

## Kurulum

python -m venv .venv
source .venv/bin/activate
pip install -r requirements.txt
uvicorn main:app --reload --port 8001

Swagger UI: http://localhost:8001/docs

## API Endpointleri

GET  /         -> Saglik kontrolu
POST /anomaly  -> Anomali tespiti (Isolation Forest)
POST /predict  -> Risk siniflandirma (Random Forest)
POST /forecast -> 7 gunluk sicaklik tahmini (LSTM)

## Model Performansi

Isolation Forest : yuzde 88 normal, yuzde 12 anomali
Random Forest    : yuzde 100 accuracy
LSTM             : MAE 2.90C, RMSE 3.96C

## Gelistirici

Kasim - ML & AI Gelistirici | Branch: feature/kasim-ai
