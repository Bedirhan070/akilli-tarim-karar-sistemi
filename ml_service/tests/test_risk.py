import sys
import os
import unittest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))
from risk_utils import risk_seviyesi_hesapla


class TestRiskSeviyesi(unittest.TestCase):

    # TEST 3
    def test_kritik_esigi_asan_skor_kritik_donmeli(self):
        # Arrange
        risk_skoru = 0.85

        # Act
        seviye, renk = risk_seviyesi_hesapla(risk_skoru)

        # Assert
        self.assertEqual(seviye, "KRITIK")
        self.assertEqual(renk, "kirmizi")

    def test_orta_araligindaki_skor_orta_donmeli(self):
        seviye, _ = risk_seviyesi_hesapla(0.55)
        self.assertEqual(seviye, "ORTA")

    def test_dusuk_skor_guvenli_donmeli(self):
        seviye, _ = risk_seviyesi_hesapla(0.20)
        self.assertEqual(seviye, "GUVENLI")

    def test_tam_sinir_deger_071_kritik_donmeli(self):
        seviye, _ = risk_seviyesi_hesapla(0.71)
        self.assertEqual(seviye, "KRITIK")

    def test_tam_sinir_deger_070_orta_donmeli(self):
        # 0.70 > 0.70 koşulunu sağlamaz → ORTA
        seviye, _ = risk_seviyesi_hesapla(0.70)
        self.assertEqual(seviye, "ORTA")


if __name__ == "__main__":
    unittest.main()
