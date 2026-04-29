from fastapi import FastAPI, HTTPException
from pydantic import BaseModel
import numpy as np
import pandas as pd
import joblib
import tensorflow as tf
from keras.models import load_model

# ==========================================
# UYGULAMA VE MODEL YUKLE
# ==========================================
app = FastAPI(
    title="Akilli Tarim Asistani - ML Servisi",
    description="Bugday icin anomali tespiti, risk skoru ve gelecek tahmini",
    version="1.0.0"
)

MODEL_PATH = "/Users/kasimozel/Desktop/Akıllı Tarım Asistanı/ml_service/models/"

# Modelleri yukle
isolation_forest = joblib.load(MODEL_PATH + "isolation_forest.pkl")
random_forest    = joblib.load(MODEL_PATH + "random_forest.pkl")
scaler           = joblib.load(MODEL_PATH + "scaler.pkl")
label_encoder    = joblib.load(MODEL_PATH + "label_encoder.pkl")
lstm_model       = load_model(MODEL_PATH + "lstm_model.keras")
lstm_scaler      = joblib.load(MODEL_PATH + "lstm_scaler.pkl")

# ==========================================
# VERI MODELLERI
# ==========================================
class HavaVerisi(BaseModel):
    max_sicaklik: float
    min_sicaklik: float
    yagis: float
    ruzgar_hizi: float

class LSTMVerisi(BaseModel):
    gunluk_veri: list  # Son 30 gunluk veri listesi
    tahmin_gun: int = 7

# ==========================================
# ENDPOINTLER
# ==========================================

@app.get("/")
def ana_sayfa():
    return {
        "mesaj": "Akilli Tarim Asistani ML Servisi Calisiyor!",
        "endpointler": ["/anomaly", "/predict", "/forecast"]
    }

@app.post("/anomaly")
def anomali_tespit(veri: HavaVerisi):
    try:
        # Veriyi hazirla
        girdi = pd.DataFrame([{
            "max_sicaklik": veri.max_sicaklik,
            "min_sicaklik": veri.min_sicaklik,
            "yagis"       : veri.yagis,
            "ruzgar_hizi" : veri.ruzgar_hizi
        }])

        # Normalize et
        girdi_normalize = pd.DataFrame(
            scaler.transform(girdi),
            columns=girdi.columns
        )

        # Tahmin yap
        sonuc = isolation_forest.predict(girdi_normalize)[0]
        skor  = isolation_forest.decision_function(girdi_normalize)[0]

        return {
            "durum"      : "normal" if sonuc == 1 else "anomali",
            "skor"       : round(float(skor), 4),
            "guveniilir" : bool(sonuc == 1),
            "mesaj"      : "Veri normal." if sonuc == 1 
                           else "Anormal veri tespit edildi!"
        }
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))


@app.post("/predict")
def risk_tahmini(veri: HavaVerisi):
    try:
        # Veriyi hazirla
        girdi = pd.DataFrame([{
            "max_sicaklik": veri.max_sicaklik,
            "min_sicaklik": veri.min_sicaklik,
            "yagis"       : veri.yagis,
            "ruzgar_hizi" : veri.ruzgar_hizi
        }])

        # Normalize et
        girdi_normalize = pd.DataFrame(
            scaler.transform(girdi),
            columns=girdi.columns
        )

        # Tahmin yap
        tahmin    = random_forest.predict(girdi_normalize)[0]
        olasilik  = random_forest.predict_proba(girdi_normalize)[0]
        siniflar  = label_encoder.classes_

        # Risk skoru
        proba_dict  = dict(zip(siniflar, olasilik))
        risk_skoru  = proba_dict.get("riskli", 0) + \
                      proba_dict.get("uygun_degil", 0)

        # Risk seviyesi
        if risk_skoru > 0.70:
            seviye = "KRITIK"
            renk   = "kirmizi"
        elif risk_skoru > 0.40:
            seviye = "ORTA"
            renk   = "sari"
        else:
            seviye = "GUVENLI"
            renk   = "yesil"

        return {
            "sinif"      : label_encoder.inverse_transform([tahmin])[0],
            "risk_skoru" : round(float(risk_skoru), 2),
            "seviye"     : seviye,
            "renk"       : renk,
            "olasiliklar": {k: round(float(v), 3) 
                           for k, v in proba_dict.items()}
        }
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))


@app.post("/forecast")
def gelecek_tahmini(veri: LSTMVerisi):
    try:
        # Son 30 gunluk veriyi al
        veri_array = np.array(veri.gunluk_veri)

        if veri_array.shape != (30, 4):
            raise HTTPException(
                status_code=400,
                detail="30 gunluk veri gerekli, her gun 4 deger olmali"
            )

        # Normalize et
        veri_normalize = lstm_scaler.transform(veri_array)

        # Gelecek tahmin
        tahminler = []
        pencere   = veri_normalize.copy()

        for _ in range(veri.tahmin_gun):
            girdi  = pencere[-30:].reshape(1, 30, 4)
            tahmin = lstm_model.predict(girdi, verbose=0)[0][0]

            yeni_satir    = pencere[-1].copy()
            yeni_satir[0] = tahmin
            pencere       = np.vstack([pencere, yeni_satir])
            tahminler.append(tahmin)

        # Gercek degerlere cevir
        tahmin_gercek = lstm_scaler.inverse_transform(
            np.concatenate([
                np.array(tahminler).reshape(-1, 1),
                np.zeros((len(tahminler), 3))
            ], axis=1)
        )[:, 0]

        return {
            "sehir"        : "Bitlis",
            "tahmin_gun"   : veri.tahmin_gun,
            "max_sicaklik_tahmini": [
                {"gun": i+1, "sicaklik": round(float(s), 1)}
                for i, s in enumerate(tahmin_gercek)
            ]
        }
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))