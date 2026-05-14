def risk_seviyesi_hesapla(risk_skoru: float) -> tuple[str, str]:
    """Risk skoru eşik karşılaştırması — pure function, model bağımlılığı yok."""
    if risk_skoru > 0.70:
        return "KRITIK", "kirmizi"
    elif risk_skoru > 0.40:
        return "ORTA", "sari"
    else:
        return "GUVENLI", "yesil"
