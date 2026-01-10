# GameHub - Aplikacja webowa do zarządzania stanem gier i wspierania rozgrywki

GameHub to aplikacja webowa stworzona w technologii ASP.NET Core MVC, służąca do przeglądania gier, tworzenia poradników, interaktywnych map oraz budowania społeczności graczy. Projekt integruje się z zewnętrznymi API (IGDB, Steam) w celu dostarczania aktualnych danych.

## Kluczowe Funkcjonalności

* **Baza Gier:** Przeglądanie i wyszukiwanie gier (integracja z IGDB API).
* **Poradniki i Wskazówki:** Tworzenie poradników z edytorem WYSIWYG, dodawanie wskazówek (Tips) oraz system oceniania.
* **Interaktywne Mapy:** Możliwość wgrywania map z gier i nanoszenia na nie punktów/znaczników (Leaflet.js).
* **Społeczność:** System komentarzy, odpowiedzi, reakcji (Like/Dislike) oraz recenzji gier.
* **Biblioteka Gracza:** Zarządzanie własną kolekcją gier, statusami (Do zagrania, Ukończone) oraz import biblioteki ze Steam.
* **Panel Administracyjny:** Zarządzanie użytkownikami, moderacja treści (zatwierdzanie/odrzucanie poradników), system zgłoszeń (Tickets).

## Technologie

* **Backend:** C# .NET 8, ASP.NET Core MVC
* **Baza Danych:** SQLite + Entity Framework Core
* **Frontend:** Razor Views, Bootstrap 5, JavaScript (jQuery)
* **API Zewn.:** IGDB (Twitch), Steam Web API
* **Dodatki:** Rotativa (PDF), Leaflet.js (Mapy), TinyMCE (Edytor tekstu)

## Wymagania wstępne

Aby uruchomić projekt lokalnie, potrzebujesz:
1.  [.NET SDK](https://dotnet.microsoft.com/download) (wersja 8.0 lub nowsza).
2.  Klucz API [IGDB (Twitch Developer)](https://dev.twitch.tv/).
3.  Klucz API [Steam Web API Key](https://steamcommunity.com/dev/apikey).

## Instalacja i Uruchomienie

1.  **Sklonuj repozytorium:**
    ```bash
    git clone 
    cd repo
    ```
    W przypadku posiadania kodu w postaci archiwum ZIP (lub folderu na dysku), pomiń krok klonowania.

2.  **Konfiguracja sekretów:**
    Otwórz plik `appsettings.json` i uzupełnij klucze API:
    ```json
    "IGDB": {
      "ClientId": "TWOJ_CLIENT_ID",
      "ClientSecret": "TWOJ_CLIENT_SECRET"
    },
    "Steam": {
      "ApiKey": "TWOJ_STEAM_API_KEY"
    }
    ```

3.  **Inicjalizacja Bazy Danych:**
    W terminalu projektu uruchom:
    ```bash
    dotnet tool install --global dotnet-ef --version 8.*
    dotnet ef migrations add InitialCreate
    dotnet ef database update
    ```
    To polecenie utworzy plik `ApplicationDB.db` i wygeneruje wszystkie tabele.

4.  **Uruchomienie aplikacji:**
    ```bash
    dotnet run --launch-profile https
    ```
    Aplikacja będzie dostępna pod adresem `https://localhost:7043`.

## Domyślne Dane Logowania Admina

Podczas pierwszego uruchomienia aplikacja sprawdza istnienie konta administratora. Jeśli nie istnieje, zostanie utworzone automatycznie:

* **Login:** `Admin`
* **Hasło:** `Admin890`

---
Autorzy: **Oliwier Ancutko** i **Patrycja Chocko**