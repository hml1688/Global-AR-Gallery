# 🖼️ Global AR Museum

**Explore the world’s art across time and space — reimagined through immersive AR and dynamic weather-based exhibitions.**

Global AR Museum is an Android-based AR application that brings art galleries into your real-world space. Whether you want to explore artworks from 19th-century Europe or experience an exhibition inspired by today’s weather, this app offers an intuitive and immersive way to engage with cultural collections.    


<div align="center" style="display: flex; justify-content: center; align-items: center; gap: 20px; margin: 20px 0;">
  <img src="https://github.com/hml1688/Global-AR-Gallery/blob/main/Images/exhibition1.jpg" style="width: 400px; height: 400px; object-fit: cover; border-radius: 8px; align-self: center;"/>
  <img src="https://github.com/hml1688/Global-AR-Gallery/blob/main/Images/exhibition2.jpg" style="width: 400px; height: 500px; object-fit: cover; border-radius: 8px; align-self: center;"/>
</div>


---

## 🧠 Purpose & Vision

Global AR Museum explores new ways of engaging with **open-access cultural data** through **immersive AR experiences**. By showcasing representative artworks from five major regions in a shared virtual space, it encourages cross-cultural exploration and artistic comparison across time and geography.  

Through its innovative **"Weather Gallery" feature**, the project also demonstrates how environmental data, such as real-time weather, can dynamically shape exhibition themes. This opens exciting possibilities for future applications, where any type of real-world data can instantly generate thematic micro-exhibitions—extending to areas like education, tourism, and public art installations.


---

## 🧭 Features at a Glance

- 🌍 **Global Gallery Mode**: Select a region and time range to generate an exhibition from real open-access collections  
- ⛅ **Weather Gallery Mode**: Use your local weather to generate exhibition themes and immersive environmental effects  
- 🏛️ **Real Museum Data**: All artworks are drawn from world-renowned collections including Harvard Art Museums and the V&A  
- 🖐️ **Embodied Navigation**: Walk through the miniature museum in your room, or zoom in/out with touch gestures  
- 🔁 **Refresh for New Artworks**: Same theme, different results — re-roll to discover more  
- 🔎 **Tap for Details**: Get contextual info about each piece — title, origin, date, and artist

---

## 🎥 App Preview

| Global Gallery Selection | AR Museum Placement | Artwork Details |
|--------------------------|---------------------|-----------------|
| ![Keyword](https://github.com/hml1688/Global-AR-Gallery/blob/main/Images/keyword-global.jpg) | ![Placement](https://github.com/hml1688/Global-AR-Gallery/blob/main/Images/globalgalleryview.jpg) | ![Details](https://github.com/hml1688/Global-AR-Gallery/blob/main/Images/globaldetails.jpg) |

| Weather-Based Keywords | Weather Effects in Action | Weather-Driven Exhibition |
|------------------------|---------------------------|---------------------------|
| ![Keywords](https://github.com/hml1688/Global-AR-Gallery/blob/main/Images/weahter-keyword.jpg) | ![Effects](https://github.com/hml1688/Global-AR-Gallery/blob/main/Images/weahter-effect5.gif) | ![Weather Gallery](https://github.com/hml1688/Global-AR-Gallery/blob/main/Images/weahter%20gallery.jpg) |

---

## 🚀 How to Use

1. Install the APK on your ARCore-compatible Android device  
2. Open the app and allow camera + location access  
3. Choose your experience:
   - **Global Gallery**: Select a region and time period
   - **Weather Gallery**: Let the app generate weather-based themes
4. Tap to place the AR gallery on a flat surface  
5. Walk around to explore, or zoom with pinch gestures  
6. Tap artworks to view details, and refresh to see more

---  

## 🏗️ System Architecture  

The project has two core exhibition modes: **Global Gallery** and **Weather Gallery**.  
Both follow the same idea: user input or environment sensing → API/JSON retrieval → artwork filtering & caching → AR frame rendering.  
The difference is **how exhibitions are triggered**.  

---  

### 🌍 Global Gallery  

Explore artworks filtered by **region** and **time period**.  

- **Input**: User selects a region + year range (validated and cached via `PlayerPrefs`).  
- **Processing**:  
  - `GalleryManager` sends parallel API queries to Harvard Art Museums + V&A.  
  - If results are too few or the network fails, the system falls back to offline JSON datasets stored in `StreamingAssets`.  
- **Output**: Artworks are mapped onto AR frames, with title, maker, and date shown in the `InfoPanel`.  

**Diagrams**:  

1. *Sequence Diagram of Data Flow in the Global Gallery*  
   <img src="https://github.com/hml1688/Global-AR-Gallery/blob/main/Images/global%20sequence%20flow%20diagram.png" width="800"/>  

2. *Flowchart of Keyword Input, Validation, and State Transfer*  
   <img src="https://github.com/hml1688/Global-AR-Gallery/blob/main/Images/global%20keyword%20flow.png" width="250"/>  

3. *End-to-End Flow of Artwork Loading*  
   <img src="https://github.com/hml1688/Global-AR-Gallery/blob/main/Images/End-to-End%20Flow%20of%20Global%20Gallery%20Artwork%20Loading.jpg" width="600"/>  


---

### ⛅ Weather Gallery  

Generate **dynamic exhibitions** based on real-time weather.  

- **Input**: Weather data fetched from OpenWeather API (GPS-based or default city: London).  
- **Processing**:  
  - Weather type (e.g., *Rain, Clouds, Sun*) is mapped to keyword pools.  
  - Keywords trigger API queries to Harvard/V&A.  
  - If APIs return too few results, offline JSON fallback is used.  
- **Output**: Weather-driven artworks are shown in AR frames, combined with matching weather effects (rain, sun, sand, etc) for immersive context.  

**Diagrams**:  
1. *Sequence Diagram of Data Flow in the Weather Gallery*  
   <img src="https://github.com/hml1688/Global-AR-Gallery/blob/main/Images/weather%20sequence%20flow%20diagram.png" width="800"/>   

2. *Flowchart of Weather Data Retrieval and Keyword Mapping*  
   <img src="https://github.com/hml1688/Global-AR-Gallery/blob/main/Images/Flowchart%20of%20Weather%20Data%20Retrieval%2C%20Keyword%20Generation%20and%20Transmission%20in%20the%20Weather%20Gallery.png" width="500"/>   

4. *End-to-End Data Flow Pipeline for Dynamic Exhibition*  
   <img src="https://github.com/hml1688/Global-AR-Gallery/blob/main/Images/weather%20ete%20data%20flow.png" width="700"/>

---  

## 📂 Main Components of Assets

The overall implementation of this project is based on the **Unity Engine**, and all core resource files are centrally stored in the `Assets` folder.  
These components include **image resources, 3D models, prefabs, scenes, scripts, and offline data cache**.  

| Directory       | Function | Key Contents (Examples) | Notes |
|-----------------|----------|--------------------------|-------|
| **Images**      | Stores UI visual assets, including page backgrounds, button icons, and decorative elements | `homepage.jpg`, `weatherBackground.jpg`, `iconApp.png`, `menuButton.png` | All set as Sprite (2D and UI) |
| **Models**      | Source files of 3D models used in the gallery and weather effects | `Model4/` (gallery model), `cloud2/`, `rain/`, `snow/` (FBX models) | Models imported from Sketchfab |
| **Prefabs**     | Reusable prefabs with fixed scale/material/Animator setup | `ArtGalleryVariant1.prefab`, `FX_Rain.prefab`, `SceneManager.prefab` | Used for consistent deployment of frames and VFX |
| **Scenes**      | Organizes functional pages and AR interaction spaces | `HomePage.unity`, `Menu.unity` (region & year input), `ARScene.unity` (gallery loading), `WeatherPrep.unity` | Managed with **SceneManager** |
| **Scripts**     | Core application logic: UI, data retrieval and offline caching, AR model placement, and AR interaction | `GalleryManagerHarvard.cs`, `ArtFrame.cs`, `InfoPanel.cs`, `WeatherGalleryManager.cs`, `TapToPlaceGallery.cs` | Fetches API data, applies textures, controls placement |
| **StreamingAssets** | Offline data cache for fallback | `offline-ham-asia-2000-2025-STRICT-CE.json`, `offline-ham-europe-2000-2025-STRICT-CE.json` | Ensures exhibition completeness and faster loading |

---


## 🔌 Data Sources

This project uses open-access APIs to retrieve artwork and environmental data:

- 🎨 [Harvard Art Museums API](https://github.com/harvardartmuseums/api-docs)
- 🏛️ [V&A Museum API](https://developers.vam.ac.uk/guide/v2/welcome.html)
- ☁️ [OpenWeather API](https://openweathermap.org/api)

---


## 📲 Get the App

👉 🔗 [Project Website](https://hml1688.github.io/Global-AR-Gallery/webpage.html)

👉 🎥 [Project Video](https://youtu.be/PBGzaap1sm8)

👉 [Download APK (v3.0)](https://github.com/hml1688/Global-AR-Gallery/releases/tag/v3.0)

> ⚠️ Requires an ARCore-supported Android device  
> 📦 Format: `.apk` (manual install)

---

## Credits

This project makes use of 3D models from Sketchfab. We gratefully credit the original creators:

- [**Art Gallery**](https://skfb.ly/oBJso) by *denis_cliofas* — licensed under [CC Attribution](http://creativecommons.org/licenses/by/4.0/).
- [**Sun**](https://skfb.ly/6yGSx) by *SebastianSosnowski* — licensed under [CC Attribution](http://creativecommons.org/licenses/by/4.0/).
- [**Cloud-sun-lowpoly**](https://skfb.ly/oGOYT) by *Mitrix* — licensed under [CC Attribution](http://creativecommons.org/licenses/by/4.0/).
- [**Snow FX Test**](https://skfb.ly/6x8TN) by *andazty* — licensed under [CC Attribution](http://creativecommons.org/licenses/by/4.0/).
- [**Rain 1**](https://skfb.ly/6TzDo) by *Paxar095* — licensed under [CC Attribution](http://creativecommons.org/licenses/by/4.0/).
- [**HYPERSPEED Starfield**](https://skfb.ly/oJ99r) by *00004707* — licensed under [CC Attribution](http://creativecommons.org/licenses/by/4.0/).


---

## 🙋 About the Creator

Developed by **Jennie Hao**  
🎓 MSc Connected Environments · University College London  
📧 [Email me](ucfnaoa@ucl.ac.uk)  

---

## 📝 License

This project is for academic and exhibition purposes only. All artwork images are sourced from publicly available, open-access APIs.

