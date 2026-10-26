# Dalmierz — monokularny pomiar odległości kamerą smartfona

Aplikacja mobilna (Unity, Android / iOS) do pasywnego pomiaru odległości do osoby
na podstawie jej wzrostu, wykonana w ramach pracy dyplomowej inżynierskiej.

## Zasada działania

Odległość wyznaczana jest z modelu kamery otworkowej:

```
d = H · f_n / r
```

- `H` – wzrost osoby odniesienia (w trybie automatycznym odcinek nos–kostka, współczynnik antropometryczny 0,896),
- `f_n` – ogniskowa znormalizowana do szerokości obrazu, wyznaczona kalibracją kamery metodą Zhanga (OpenCV),
- `r` – wysokość sylwetki na obrazie jako ułamek szerokości obrazu.

## Tryby pomiaru

| Tryb | Opis |
|---|---|
| Wzrost – ręczny | Użytkownik ustawia dwie linie na czubku głowy i stopach osoby. |
| Wzrost – automatyczny | Punkty charakterystyczne sylwetki wyznacza sieć MoveNet Thunder na wycinku kadru śledzącym osobę; detektor YOLO11n odnajduje osobę w kadrze. |
| AR | Raycast ARCore / ARKit do punktu w środku ekranu – tryb referencyjny do porównań. |

## Wymagania

- Unity 6 (6000.0.44f1)
- AR Foundation 6.5 z ARCore (Android) i ARKit (iOS)
- Unity Inference Engine 2.6.1
- Android 11 (API 30) lub nowszy z obsługą ARCore; iPhone z obsługą ARKit

## Wyniki pomiarów

Każde uruchomienie aplikacji tworzy plik `pomiary_RRRRMMDD_GGMMSS.csv` (separator `;`):

- Android: `Android/data/<identyfikator aplikacji>/files/`
- iOS: aplikacja Pliki → Na moim iPhonie → Dalmierz

## Modele i licencje

| Model | Zastosowanie | Autor | Licencja | Źródło |
|---|---|---|---|---|
| MoveNet SinglePose Thunder | punkty charakterystyczne sylwetki | Google | Apache 2.0 | TensorFlow Hub / Kaggle Models |
| YOLO11n | wykrywanie osoby w kadrze | Ultralytics | AGPL-3.0 | [unity/inference-engine-yolo](https://huggingface.co/unity/inference-engine-yolo) |

## Licencja

Kod aplikacji udostępniony jest na licencji **GNU AGPL-3.0** (plik [LICENSE](LICENSE)),
co wynika z wykorzystania modelu YOLO11n firmy Ultralytics.

---

Autor: Oskar Skowroński · Akademia Nauk Stosowanych w Elblągu · 2026
