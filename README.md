# 🌕 Pouso na Lua — Experiência VR (Cardboard & URP)

Uma experiência imersiva de realidade virtual lunar desenvolvida em **Unity 6**, utilizando o **Universal Render Pipeline (URP)** e o **Google Cardboard XR Plugin**. O jogador explora um ambiente lunar com relevo realista, gravidade reduzida, suporte a gamepad Bluetooth e física interativa para manipulação de rochas e detritos.

---

## 🚀 Destaques do Projeto

- **👓 Realidade Virtual Mobile Acessível:** Otimizado para smartphones Android usando Google Cardboard (visores VR móveis).
- **🕹️ Locomoção VR Dinâmica na Direção do Olhar:** Caminhe livremente com o analógico do controle para onde sua cabeça estiver apontando no visor VR.
- **🏔️ Acompanhamento Suave de Relevo:** Sistema baseado em `CharacterController` com projeção vetorial no plano inclinado (`ProjectOnPlane`), evitando que o jogador voe ou fique preso ao subir ou descer crateras e encostas.
- **🪨 Física Interativa estilo Half-Life 2 / Garry's Mod:**
  - Mire em qualquer rocha ou regolito lunar para destacá-la.
  - Puxe e segure objetos levitando na sua mira.
  - Ajuste a distância do objeto flutuante com o controle.
  - Arremesse ou solte pedras para observar a física e a gravidade lunar.
- **🌌 Skybox Espacial e Atmosfera Lunar:** Céu estrelado profundo com o planeta Terra visível no horizonte e gravidade adaptada para a Lua (~$1.62\text{ m/s}^2$).

---

## 🎮 Controles e Gameplay

O sistema suporta **qualquer controle Bluetooth/USB** (Xbox, PlayStation, Ipega, controles estilo "VR Box" com analógico) além de teclado/mouse para testes rápidos no editor do Unity.

| Ação | Controle / Gamepad | Teclado & Mouse (Editor) |
| :--- | :--- | :--- |
| **Andar** | Analógico Esquerdo *(move na direção da cabeça)* | `W`, `A`, `S`, `D` |
| **Girar Câmera (Snap Turn)** | Analógico Direito (Esquerda / Direita) | Setas `←` e `→` |
| **Pegar Pedra** | Botão Principal (`A`, `B`, `X`, `Y`, `RB`, `LB`) | Teclas `E` ou `F` |
| **Soltar Pedra** | Botão `A`, `B`, `Y`, `RB`, `LB` | Teclas `E` ou `F` |
| **Arremessar Pedra** | Gatilho `RT` / `LT` ou Botão `X` | Botão Esquerdo do Mouse |
| **Aproximar / Afastar Pedra** | Analógico Direito (Cima/Baixo) ou `D-Pad` | Roda do Mouse ou Setas `↑` / `↓` |
| **Olhar ao Redor (Debug)** | Giro do Headset (Giroscópio) | Segurar Botão Direito + Mover Mouse |

---

## 🛠️ Tecnologias Utilizadas

- **Engine:** Unity `6000.6.7f1` (Unity 6)
- **Render Pipeline:** Universal Render Pipeline (URP `17.6.0`)
- **XR SDK:** Google Cardboard XR Plugin for Unity (`1.36.0`)
- **Input:** Unity New Input System (`1.20.1`)
- **Plataforma Alvo:** Android (Vulkan / OpenGL ES 3, API 26+)
- **Versionamento:** Git + Git LFS (Large File Storage)

---

## 📁 Estrutura do Código (`Assets/PousoLua/`)

O código foi projetado de forma modular e desacoplada:

- **`VRInput.cs`:** Centraliza a leitura das entradas do novo Input System, mapeando gamepads genéricos, joysticks Bluetooth e atalhos de depuração no computador.
- **`VRPlayerMovement.cs`:** Gerencia a locomoção do jogador, amortecimento vertical da câmera, velocidade de caminhada, alinhamento com a normal do terreno e aderência ao chão (*ground snapping*).
- **`VRObjectGrabber.cs`:** Controlador de campo gravitacional colocado na câmera. Calcula trajetória de atração, conservação de momento, sustentação na mira e impulso de arremesso.
- **`Grabbable.cs`:** Componente aplicado a objetos e pedras. Controla colisões dinâmicas e o realce visual (highlight via MaterialPropertyBlock) quando o jogador mira no objeto.
- **`RockMeshGenerator.cs`:** Gerador procedural de malhas de pedra *low-poly* usando deformação facetada em icosferas.
- **`PousoLuaBootstrap.cs`:** Inicializador automático que executa em runtime e no Editor, garantindo que o mapa e os componentes estejam sempre conectados sem exigir configurações manuais na cena.

---

## ⚙️ Como Abrir e Executar o Projeto

### Pré-requisitos
1. **Unity Hub** instalado.
2. **Unity 6 (6000.6.x)** com o módulo **Android Build Support** (Android SDK & NDK Tools e OpenJDK).
3. **Git** com extensão **Git LFS** instalada:
   ```bash
   git lfs install
   ```

### Clonando o Repositório
```bash
git clone https://github.com/Matheusprower/PousoNaLuaA3.git
cd PousoNaLuaA3
git lfs pull
```

### Abrindo no Unity
1. No **Unity Hub**, clique em **Add** -> **Add project from disk**.
2. Selecione a pasta do projeto.
3. Abra o projeto e carregue a cena principal em:  
   `Assets/Samples/Google Cardboard XR Plugin for Unity/1.36.0/Hello Cardboard/Scenes/HelloCardboard.unity`
4. Pressione o botão **Play** no Editor para testar com teclado e mouse, ou conecte seu smartphone Android via cabo USB e selecione **File** -> **Build and Run**.

---

## 📱 Configurações do Android & Build

- **Scripting Backend:** IL2CPP (com suporte a arquitetura `ARM64`).
- **Graphics API:** Vulkan com fallback para OpenGLES3.
- **Android Gradle Plugin / Manifest:** Configurado com `Activity` padrão no AndroidManifest e dependências do Cardboard injetadas via `mainTemplate.gradle`.

---

## 📄 Licença

Este projeto é desenvolvido para fins educacionais e de demonstração em realidade virtual móvel.
