# Retro RPG Reconstruction Framework

## Objetivo

Construir um framework em **Unity 6.5 + C#** capaz de ler dados de ROMs de RPGs retrô, convertê-los para uma **representação intermediária (IR)** própria e gerar assets utilizáveis pela Unity.

O primeiro alvo será **Pokémon FireRed (GBA)**, começando por uma reconstrução mínima e progressiva de **Pallet Town**. A arquitetura não deve ser específica de Pokémon: o objetivo é permitir a adição futura de adaptadores para outros jogos, como **Pokémon Emerald**, **Medabots** e, posteriormente, jogos de **Nintendo DS**, como Digimon.

> Princípio central: o núcleo do projeto não deve conhecer regras específicas de Pokémon. Regras, formatos e offsets específicos de cada jogo devem ficar isolados em adaptadores/importadores.

---

## Escopo inicial

### Incluído

- Unity 6.5.
- C#.
- Editor tooling dentro da Unity.
- Leitura de ROM `.gba` fornecida localmente pelo usuário.
- Detecção do jogo/ROM.
- Parser de estruturas conhecidas.
- Extração de tiles, paletas, sprites, mapas, eventos, textos e warps quando suportados.
- Conversão para um IR próprio.
- Geração de assets Unity.
- Geração de Tilemaps.
- Visualizador de mapas no Editor.
- Movimento em grid.
- Colisão.
- NPCs.
- Diálogos.
- Warps/interiores.
- Encontros aleatórios.
- Sistema de batalha em fase posterior.
- Arquitetura preparada para renderer clássico e renderer HD-2D no futuro.

### Fora do escopo inicial

- Portar o jogo inteiro de uma só vez.
- Criar um emulador de GBA.
- Executar código original da ROM diretamente.
- Reproduzir byte a byte toda a engine original.
- Suporte inicial a Nintendo DS.
- HD-2D antes do renderer clássico funcionar.
- Distribuir ROMs ou assets proprietários junto do projeto.

---

# Arquitetura

## Pipeline principal

```text
FireRed.gba
    |
    v
+--------------------------+
| GBA ROM Importer         |
+--------------------------+
| Header Parser            |
| Tileset Extractor        |
| Palette Extractor        |
| Sprite Extractor         |
| Map Parser               |
| Event Parser             |
| Text Parser              |
+------------+-------------+
             |
             v
         RetroRPG IR
             |
       +-----+-----+
       |           |
       v           v
   JSON/Data     PNG Assets
       |           |
       +-----+-----+
             |
             v
       Unity Importer
             |
             v
+--------------------------+
| Unity Assets             |
+--------------------------+
| Tilemaps                 |
| Sprites                  |
| ScriptableObjects        |
| NPC Prefabs              |
| Collision                |
| Warps                    |
| Dialogues                |
+--------------------------+
```

---

## Camadas

### 1. `RetroRPG.Core`

Responsável por tipos e regras genéricas do framework.

Não pode depender de Pokémon, FireRed, Medabots ou offsets específicos de ROM.

Entidades previstas:

```text
Character
Creature
BattleEntity
Item
Skill
NPC
Map
MapEvent
Warp
Dialogue
Encounter
Inventory
Party
SaveGame
```

### 2. `RetroRPG.IR`

Representação intermediária independente da ROM e da Unity sempre que possível.

Exemplos:

```text
GameDefinition
MapDefinition
TilesetDefinition
PaletteDefinition
SpriteDefinition
NpcDefinition
DialogueDefinition
WarpDefinition
EncounterTableDefinition
CreatureDefinition
ItemDefinition
SkillDefinition
```

Objetivo: permitir que diferentes importadores gerem o mesmo modelo de saída.

### 3. `RetroRPG.Importers`

Adaptadores responsáveis por compreender os formatos de cada ROM.

Estrutura desejada:

```text
RetroRPG.Importers/
└── GBA/
    ├── Common/
    ├── PokemonFireRed/
    ├── PokemonEmerald/
    └── Medabots/

# Futuro
└── NDS/
    └── Digimon/
```

### 4. `RetroRPG.Unity`

Responsável pela conversão do IR em objetos Unity.

Exemplos:

```text
TilemapBuilder
SpriteAssetBuilder
ScriptableObjectBuilder
NpcPrefabBuilder
CollisionBuilder
WarpBuilder
DialogueAssetBuilder
```

### 5. `RetroRPG.Editor`

Ferramentas de editor.

Inclui:

- ROM Importer Window.
- ROM Inspector.
- Map Browser.
- Map Preview.
- Import logs.
- Reimport.
- Diagnóstico de estruturas não suportadas.

### 6. `RetroRPG.Runtime`

Sistemas necessários para executar a reconstrução na Unity.

Inclui progressivamente:

```text
GridMovementSystem
MapTransitionSystem
InteractionSystem
DialogueSystem
NpcSystem
EncounterSystem
BattleSystem
SaveSystem
```

---

# Estrutura sugerida do repositório

```text
Assets/
├── RetroRPG/
│   ├── Core/
│   ├── IR/
│   ├── Runtime/
│   ├── Unity/
│   ├── Editor/
│   ├── Importers/
│   │   └── GBA/
│   │       ├── Common/
│   │       └── PokemonFireRed/
│   └── Tests/
│       ├── EditMode/
│       └── PlayMode/
│
├── Imported/
│   └── FireRed/
│       ├── Maps/
│       ├── Tilesets/
│       ├── Sprites/
│       ├── Creatures/
│       ├── Trainers/
│       ├── Items/
│       ├── Moves/
│       ├── Dialogues/
│       └── Prefabs/
│
└── Scenes/
    ├── Bootstrap.unity
    └── Sandbox.unity

docs/
├── ARCHITECTURE.md
├── ROM_FORMAT.md
├── IR_SPEC.md
├── IMPORT_PIPELINE.md
├── AI_WORKFLOW.md
└── DECISIONS.md

AGENTS.md

.codex/
├── config.toml
└── agents/
    ├── rrpg-architect.toml
    ├── rom-analyst.toml
    ├── parser-worker.toml
    ├── unity-worker.toml
    ├── test-worker.toml
    ├── docs-worker.toml
    └── milestone-reviewer.toml

.agents/
└── skills/
    ├── rrpg-orchestrator/
    │   └── SKILL.md
    ├── plan-current-mvp/
    │   └── SKILL.md
    ├── analyze-rom-structure/
    │   └── SKILL.md
    ├── implement-unity-task/
    │   └── SKILL.md
    └── validate-milestone/
        └── SKILL.md
```

---

# Regras de implementação para o Codex

1. Não acoplar `RetroRPG.Core` a Pokémon.
2. Toda regra específica de FireRed deve ficar em `Importers/GBA/PokemonFireRed`.
3. Evitar valores mágicos espalhados pelo código.
4. Offsets, tabelas e formatos conhecidos devem ser declarados em estruturas próprias e documentados.
5. Toda leitura binária deve validar limites antes de acessar bytes.
6. Falhas de parsing devem gerar erros descritivos, nunca exceções genéricas sem contexto.
7. Importadores devem produzir IR; não devem instanciar GameObjects diretamente.
8. A criação de objetos Unity deve acontecer apenas na camada Unity/Editor.
9. O runtime deve consumir assets já importados, não ler a ROM durante gameplay.
10. Cada MVP deve ser utilizável e demonstrável antes do próximo.
11. Não iniciar sistema de batalha antes de mapa, movimento, transições, NPCs e diálogos estarem funcionando.
12. Escrever testes EditMode para parsers e IR.
13. Escrever PlayMode tests apenas quando houver comportamento runtime relevante.
14. Manter logs de importação estruturados por etapa.
15. Não incluir ROM nem assets proprietários no repositório.

---

# Estratégia de IA, modelos, agents e skills no Codex/VS Code

## Objetivo

Usar o Codex no VS Code como uma equipe especializada, em vez de utilizar um único modelo para todas as tarefas.

A divisão deve seguir três princípios:

1. **Agents definem papel, modelo, reasoning e permissões.**
2. **Skills definem workflows repetíveis.**
3. **`AGENTS.md` define regras persistentes e limites arquiteturais do repositório.**

A orquestração deve privilegiar qualidade nas decisões estruturais e custo/velocidade na execução mecânica.

---

## Política de modelos

### GPT-5.6 Sol

Usar para:

- arquitetura;
- planejamento de MVP;
- decisões difíceis de design;
- análise de formatos binários;
- reverse engineering de ROM;
- investigação de offsets, ponteiros e estruturas;
- bugs difíceis;
- revisão arquitetural;
- revisão de milestones;
- situações em que múltiplas hipóteses precisam ser comparadas.

Configuração recomendada:

```text
Modelo: gpt-5.6-sol
Reasoning padrão: high
Reasoning para arquitetura/reverse engineering: xhigh
Escalonamento excepcional: max/ultra quando suportado pelo cliente e a tarefa justificar
```

Não usar Sol como padrão para geração repetitiva de boilerplate.

### GPT-5.6 Terra

Usar como executor principal.

Adequado para:

- implementação C#;
- Unity Editor tooling;
- parsers cujo formato já foi documentado;
- criação de ScriptableObjects;
- criação de builders;
- runtime;
- refactors moderados;
- correção de bugs já diagnosticados;
- implementação de testes não triviais.

Configuração recomendada:

```text
Modelo: gpt-5.6-terra
Reasoning padrão: medium
Reasoning para implementação complexa: high
```

### GPT-5.6 Luna

Usar para tarefas estreitas, repetitivas e de alto volume.

Adequado para:

- boilerplate;
- fixtures;
- testes simples;
- documentação;
- XML docs;
- organização de namespaces;
- renames;
- warnings;
- pequenos refactors mecânicos;
- atualização de checklists;
- sumarização de logs;
- tarefas de leitura paralela simples.

Configuração recomendada:

```text
Modelo: gpt-5.6-luna
Reasoning padrão: low ou medium
```

---

## Matriz de roteamento de tarefas

| Tipo de tarefa | Agent | Modelo | Reasoning | Escrita |
|---|---|---|---|---|
| Planejamento de MVP | `rrpg_architect` | GPT-5.6 Sol | xhigh | não |
| Arquitetura/IR | `rrpg_architect` | GPT-5.6 Sol | xhigh | não |
| Reverse engineering da ROM | `rom_analyst` | GPT-5.6 Sol | xhigh | não |
| Implementação de parser documentado | `parser_worker` | GPT-5.6 Terra | high | sim |
| Implementação Unity/C# | `unity_worker` | GPT-5.6 Terra | high | sim |
| Testes/fixtures/refactor mecânico | `test_worker` | GPT-5.6 Luna | medium | sim |
| Documentação e sincronização | `docs_worker` | GPT-5.6 Luna | medium | sim |
| Review técnico de milestone | `milestone_reviewer` | GPT-5.6 Sol | high | não |
| Debug complexo sem causa conhecida | `rrpg_architect` | GPT-5.6 Sol | xhigh | inicialmente não |

Regra:

> O modelo de maior capacidade deve ser usado para descobrir **o que** deve ser feito. O modelo executor deve implementar **como** foi decidido. O modelo barato deve cuidar do trabalho repetível.

---

# Configuração multi-agent do Codex

O projeto deve versionar configuração específica do Codex em `.codex/`.

## `.codex/config.toml`

Configuração inicial sugerida:

```toml
[agents]
enabled = true
max_concurrent_threads_per_session = 4
default_subagent_model = "gpt-5.6-terra"
default_subagent_reasoning_effort = "medium"
```

A concorrência inicial deve ser conservadora.

Aumentar somente após o workflow provar que os agentes não estão:

- editando os mesmos arquivos simultaneamente;
- duplicando investigação;
- produzindo decisões conflitantes;
- poluindo o repositório com mudanças de escopo.

---

# Custom agents do projeto

Os agents abaixo devem ser criados em `.codex/agents/`.

## `rrpg-architect.toml`

Responsável por planejamento, arquitetura e decisões que afetam múltiplas camadas.

```toml
name = "rrpg_architect"
description = "Architect and planner for RetroRPG. Use for MVP planning, IR contracts, architecture decisions, hard debugging and cross-layer design."
model = "gpt-5.6-sol"
model_reasoning_effort = "xhigh"
sandbox_mode = "read-only"

developer_instructions = '''
Operate as the technical architect of RetroRPG.

Read ROADMAP_CODEX_UPDATED.md and the applicable AGENTS.md files before making recommendations.

Do not implement code.

Keep RetroRPG.Core and RetroRPG.IR independent from Pokemon, FireRed and ROM-specific details.

For each architectural task:
1. identify the current MVP;
2. inspect existing code and docs;
3. state constraints and assumptions;
4. propose the smallest design that satisfies the current MVP;
5. define contracts and acceptance criteria;
6. identify work that can be delegated to implementation agents.

Do not design future systems unless required by the current milestone.
'''
```

## `rom-analyst.toml`

Responsável pelo reverse engineering e pela investigação de estruturas da ROM.

```toml
name = "rom_analyst"
description = "Read-only GBA ROM reverse-engineering specialist. Use to investigate binary layouts, pointers, offsets, maps, palettes, tiles, events and FireRed-specific structures before parser implementation."
model = "gpt-5.6-sol"
model_reasoning_effort = "xhigh"
sandbox_mode = "read-only"

developer_instructions = '''
Investigate ROM structures; do not implement production code.

Work from evidence.

For every conclusion:
- identify ROM region, pointer or structure involved when known;
- distinguish verified facts from hypotheses;
- validate bounds and pointer conversions;
- describe how the structure should map into RetroRPG IR;
- document unknowns explicitly.

Never distribute ROM bytes or proprietary extracted assets.

Prefer producing a parser specification that parser_worker can implement.
'''
```

## `parser-worker.toml`

Responsável por implementar parsers depois que o formato estiver suficientemente entendido.

```toml
name = "parser_worker"
description = "Implementation agent for ROM readers and parsers after the binary format has been documented."
model = "gpt-5.6-terra"
model_reasoning_effort = "high"
sandbox_mode = "workspace-write"

developer_instructions = '''
Implement only the parser task assigned by the parent agent.

Follow the documented ROM format and current IR contract.

Requirements:
- validate every binary read;
- avoid magic values outside named format definitions;
- produce descriptive parsing errors;
- never instantiate Unity GameObjects;
- add or update EditMode tests;
- do not expand scope to later roadmap phases.

Run relevant tests before returning.
'''
```

## `unity-worker.toml`

Responsável pela implementação Unity/C#.

```toml
name = "unity_worker"
description = "Unity 6.5 and C# implementation agent for Editor tooling, asset builders and runtime systems."
model = "gpt-5.6-terra"
model_reasoning_effort = "high"
sandbox_mode = "workspace-write"

developer_instructions = '''
Implement the assigned Unity/C# task with the smallest coherent patch.

Respect assembly boundaries.

ROM-specific parsing belongs to Importers.
Unity object creation belongs to Unity/Editor.
Runtime must consume imported assets and must not read the ROM.

Add tests when practical.
Do not implement future MVP features.
Run the relevant validation before returning.
'''
```

## `test-worker.toml`

Responsável por testes, fixtures e trabalho mecânico.

```toml
name = "test_worker"
description = "Focused test and mechanical-refactor worker. Use for unit tests, fixtures, warnings and narrow repetitive changes."
model = "gpt-5.6-luna"
model_reasoning_effort = "medium"
sandbox_mode = "workspace-write"

developer_instructions = '''
Stay narrowly scoped.

Prefer tests that prove externally observable behavior and parser contracts.

Do not redesign production architecture.
Do not weaken assertions to make tests pass.
If a failure appears architectural or ambiguous, stop and return evidence to the parent agent.
'''
```

## `docs-worker.toml`

```toml
name = "docs_worker"
description = "Documentation synchronizer for architecture, ROM format, IR contracts, decisions and roadmap status."
model = "gpt-5.6-luna"
model_reasoning_effort = "medium"
sandbox_mode = "workspace-write"

developer_instructions = '''
Update documentation only from verified implementation state or explicit architectural decisions.

Keep ROADMAP_CODEX_UPDATED.md, ARCHITECTURE.md, ROM_FORMAT.md, IR_SPEC.md and DECISIONS.md consistent.

Never mark a roadmap item complete without validation evidence.
'''
```

## `milestone-reviewer.toml`

Responsável pelo quality gate de cada MVP.

```toml
name = "milestone_reviewer"
description = "Read-only milestone reviewer. Use after implementation to check architecture, correctness, regressions, tests and roadmap acceptance criteria."
model = "gpt-5.6-sol"
model_reasoning_effort = "high"
sandbox_mode = "read-only"

developer_instructions = '''
Review the current milestone as a code owner.

Check:
- acceptance criteria;
- architectural boundaries;
- correctness;
- binary safety;
- test quality;
- regressions;
- undocumented assumptions;
- accidental implementation of future scope.

Lead with concrete findings.

Do not approve a milestone because the code merely compiles.
Do not modify files.
'''
```

---

# Skills do workspace

Repo-scoped skills devem ficar em:

```text
.agents/skills/<skill-name>/SKILL.md
```

Cada skill deve ter:

```text
---
name: ...
description: ...
---

instruções...
```

As descriptions devem ser específicas o suficiente para o Codex conseguir selecionar automaticamente a skill correta.

---

## Skill `rrpg-orchestrator`

Arquivo:

```text
.agents/skills/rrpg-orchestrator/SKILL.md
```

Objetivo:

Orquestrar o trabalho do roadmap sem permitir que um único agent implemente tudo indiscriminadamente.

Comportamento esperado:

```text
1. Ler ROADMAP_CODEX_UPDATED.md.
2. Localizar a primeira fase incompleta.
3. Não avançar para fases posteriores.
4. Avaliar se a tarefa é arquitetura, investigação, implementação, teste ou review.
5. Delegar para o custom agent apropriado.
6. Manter o main thread focado em decisões e síntese.
7. Paralelizar somente tarefas independentes.
8. Após implementação, executar validação.
9. Chamar milestone_reviewer antes de fechar um MVP.
10. Atualizar documentação/checklist somente após aprovação.
```

Template sugerido:

```markdown
---
name: rrpg-orchestrator
description: Orchestrate implementation of the current RetroRPG roadmap milestone using the specialized Codex agents. Use when asked to continue, implement, execute or advance ROADMAP_CODEX_UPDATED.md.
---

Read ROADMAP_CODEX_UPDATED.md and applicable AGENTS.md instructions.

Work only on the earliest incomplete milestone unless the user explicitly selects another one.

Classify each work item:

- architecture/design -> rrpg_architect
- ROM investigation -> rom_analyst
- parser implementation -> parser_worker
- Unity/C# implementation -> unity_worker
- tests/mechanical work -> test_worker
- docs synchronization -> docs_worker
- milestone gate -> milestone_reviewer

Prefer read-only investigation before code changes when the binary format or architecture is uncertain.

Parallelize independent read-only tasks where useful.
Do not run concurrent writers against the same subsystem.

Before marking a milestone complete:
1. run relevant tests;
2. compare results with acceptance criteria;
3. obtain milestone_reviewer findings;
4. fix blocking findings;
5. update docs and ROADMAP_CODEX_UPDATED.md.

Return a concise summary of:
- tasks completed;
- files changed;
- tests run;
- unresolved risks;
- next eligible roadmap task.
```

---

## Skill `plan-current-mvp`

Usar quando uma nova fase estiver prestes a começar.

Deve:

- encontrar o MVP atual;
- inspecionar arquitetura existente;
- produzir tarefas pequenas;
- indicar dependências;
- escolher agent/model de cada tarefa;
- indicar tarefas paralelizáveis;
- definir testes;
- não escrever código.

Agent preferencial: `rrpg_architect`.

---

## Skill `analyze-rom-structure`

Usar para investigação binária.

Deve exigir:

- hipótese;
- evidência;
- offset/range quando aplicável;
- interpretação do ponteiro;
- bounds check;
- resultado esperado;
- impacto no IR;
- nível de confiança;
- perguntas ainda abertas.

Agent preferencial: `rom_analyst`.

---

## Skill `implement-unity-task`

Usar para tarefas Unity/C# já definidas.

Deve:

- respeitar asmdefs;
- manter dependências unidirecionais;
- limitar mudanças ao escopo da tarefa;
- executar testes;
- reportar arquivos alterados;
- não antecipar features.

Agent preferencial: `unity_worker`.

Quando a tarefa for parser puro, usar `parser_worker` no lugar de `unity_worker`.

---

## Skill `validate-milestone`

Usar antes de fechar cada MVP.

Pipeline:

```text
tests
  ↓
static/compile checks
  ↓
milestone_reviewer
  ↓
correções
  ↓
retest
  ↓
docs-worker
  ↓
marcar checklist
```

O milestone só pode ser concluído se não houver finding bloqueante.

---

# `AGENTS.md` do repositório

Criar `AGENTS.md` na raiz com as regras permanentes do projeto.

Conteúdo mínimo:

```markdown
# RetroRPG Codex Instructions

## Source of truth

- ROADMAP_CODEX_UPDATED.md defines scope and implementation order.
- Work on the earliest incomplete milestone unless the user explicitly selects another milestone.
- Do not silently expand scope.

## Architecture

- RetroRPG.Core and RetroRPG.IR must remain game-agnostic.
- FireRed-specific rules belong in Importers/GBA/PokemonFireRed.
- Importers emit IR and never create Unity GameObjects.
- Unity/Editor converts IR into Unity assets.
- Runtime consumes imported assets and never parses the ROM during gameplay.

## ROM safety

- Validate bounds before every binary read.
- Keep offsets, signatures and pointer rules in named format definitions.
- Distinguish verified ROM facts from hypotheses.
- Never commit ROMs or proprietary extracted assets.

## Workflow

- Use rrpg_architect for architecture and planning.
- Use rom_analyst when ROM structures are uncertain.
- Use parser_worker for parser implementation after the format is understood.
- Use unity_worker for Unity/C# implementation.
- Use test_worker for tests and narrow mechanical work.
- Use milestone_reviewer before marking an MVP complete.
- Keep the main agent focused on orchestration and synthesis.

## Validation

- Run relevant tests after changes.
- Do not mark a checkbox complete without evidence.
- Update docs when contracts or verified ROM knowledge changes.
```

---

# `AGENTS.md` especializados

Após a estrutura inicial existir, considerar instruções adicionais próximas ao código.

## `Assets/RetroRPG/Core/AGENTS.md`

Deve reforçar:

```text
No Pokemon/FireRed-specific names or rules.
No UnityEditor dependency.
Prefer generic domain terms.
Breaking public contracts require architectural review.
```

## `Assets/RetroRPG/Importers/GBA/AGENTS.md`

Deve reforçar:

```text
Bounds check every read.
Document pointer/address conversion.
No Unity GameObject creation.
Unknown data must not be guessed silently.
Every discovered format should update ROM_FORMAT.md.
```

## `Assets/RetroRPG/Editor/AGENTS.md`

Deve reforçar:

```text
Editor code may consume IR/importer APIs.
Editor code must not leak into Runtime assemblies.
Generated assets must be deterministic where practical.
Reimport must be safe.
```

---

# Workflow multi-agent por tarefa

## Nova fase

```text
main agent
   |
   v
rrpg_architect (Sol/xhigh)
   |
   +--> plano
   +--> contratos
   +--> critérios de aceite
   |
   v
workers
```

## Reverse engineering

```text
main
  |
  v
rom_analyst (Sol/xhigh, read-only)
  |
  v
ROM format specification
  |
  v
parser_worker (Terra/high)
  |
  v
test_worker (Luna/medium)
  |
  v
milestone_reviewer (Sol/high)
```

## Feature Unity

```text
rrpg_architect
      |
      v
unity_worker (Terra/high)
      |
      v
test_worker (Luna/medium)
      |
      v
milestone_reviewer (Sol/high)
```

---

# Regras de paralelização

Pode paralelizar:

- leitura de documentação;
- mapeamento de código;
- análise de logs;
- investigação de estruturas independentes;
- geração de testes que não tocam os mesmos arquivos;
- revisão read-only.

Evitar paralelizar:

- dois agents editando o mesmo parser;
- dois agents modificando o mesmo contrato IR;
- mudanças simultâneas em asmdefs;
- migrations/refactors amplos;
- decisões arquiteturais concorrentes.

Regra:

> Paralelismo deve reduzir tempo sem introduzir ambiguidade de ownership.

Limite inicial recomendado:

```text
até 4 subagents simultâneos;
preferencialmente 1 writer por subsystem;
reviews sempre após convergência das mudanças.
```

---

# Escalonamento de modelo

O orchestrator pode escalar uma tarefa quando:

- duas tentativas do worker falharem pela mesma causa;
- houver inconsistência entre documentação e bytes observados;
- um contrato IR precisar mudar;
- um bug atravessar mais de duas camadas;
- houver risco de quebrar compatibilidade;
- a causa raiz continuar desconhecida.

Fluxo:

```text
Luna -> Terra -> Sol/high -> Sol/xhigh -> Sol/max/ultra quando suportado e realmente necessário
```

Não escalar apenas porque um teste falhou uma vez.

---

# Uso no VS Code

O workflow preferencial é através do Codex IDE extension.

### Invocação explícita de skill

Usar o seletor de skills ou mencionar:

```text
$rrpg-orchestrator
```

Exemplo:

```text
Use $rrpg-orchestrator para continuar o primeiro milestone incompleto do ROADMAP_CODEX_UPDATED.md.
Planeje antes de editar, delegue para os agents especializados e só marque tarefas concluídas depois dos testes e do milestone review.
```

### Invocação explícita de agents

Para investigações específicas:

```text
Delegue a investigação do formato de map header para rom_analyst.
Depois passe a especificação validada para parser_worker implementar.
Use test_worker para os testes e milestone_reviewer para o gate final.
```

O fluxo deve continuar funcional mesmo que a UI visual de background agents mude, pois a fonte de verdade fica versionada no repositório.

---

# Fallback sem multi-agent

Se uma versão do cliente não disponibilizar custom agents/subagents:

1. manter as Skills e `AGENTS.md`;
2. executar o mesmo workflow sequencialmente;
3. selecionar manualmente o modelo recomendado para cada etapa;
4. registrar no prompt qual papel está sendo executado;
5. manter os mesmos gates de teste e review.

Exemplo:

```text
Sol/xhigh -> planejamento
Terra/high -> implementação
Luna/medium -> testes/docs
Sol/high -> review
```

A arquitetura do processo não deve depender da UI do Codex.

---

# Referências operacionais do Codex

Antes de alterar a infraestrutura de agents/skills, verificar a documentação oficial atual:

```text
https://developers.openai.com/codex/guides/agents-md
https://developers.openai.com/codex/skills
https://learn.chatgpt.com/docs/agent-configuration/subagents
https://developers.openai.com/codex/config-reference
https://developers.openai.com/api/docs/guides/latest-model
```

Os nomes de modelos, níveis de reasoning e formatos de configuração podem evoluir. Se a documentação atual divergir deste roadmap, atualizar `docs/AI_WORKFLOW.md` e a configuração antes de continuar.

---

# Modelo de IR mínimo

## `GameDefinition`

```csharp
public sealed class GameDefinition
{
    public string Id;
    public string Title;
    public string Platform;
    public List<MapDefinition> Maps;
}
```

## `MapDefinition`

```csharp
public sealed class MapDefinition
{
    public string Id;
    public string Name;
    public int Width;
    public int Height;
    public string TilesetId;
    public List<int> Tiles;
    public List<NpcDefinition> Npcs;
    public List<WarpDefinition> Warps;
    public List<MapEventDefinition> Events;
}
```

## `NpcDefinition`

```csharp
public sealed class NpcDefinition
{
    public string Id;
    public string SpriteId;
    public int X;
    public int Y;
    public string Direction;
    public string MovementType;
    public string EventId;
}
```

## `WarpDefinition`

```csharp
public sealed class WarpDefinition
{
    public int X;
    public int Y;
    public string TargetMapId;
    public int TargetX;
    public int TargetY;
}
```

Os modelos acima são iniciais e podem evoluir sem quebrar a separação entre importador, IR e Unity.

---

# Editor Tooling

## Retro RPG Importer

Criar uma janela de Editor semelhante a:

```text
+-------------------------------------+
|        RETRO RPG IMPORTER           |
+-------------------------------------+
| ROM: pokemon_firered.gba            |
|                                     |
| Game detected: Pokemon FireRed      |
| Platform: GBA                       |
|                                     |
| [x] Maps                            |
| [x] Tilesets                        |
| [x] Sprites                         |
| [x] Palettes                        |
| [x] NPCs                            |
| [x] Dialogues                       |
| [x] Warps                           |
| [ ] Battles                         |
|                                     |
|          [ IMPORT ROM ]             |
+-------------------------------------+
```

### A janela deve permitir

- selecionar arquivo `.gba`;
- identificar a ROM;
- mostrar metadados básicos;
- selecionar categorias de importação;
- importar;
- reimportar;
- exibir progresso;
- exibir warnings;
- exibir erros;
- abrir a pasta gerada;
- abrir o Map Browser após importação.

---

# Map Browser

Criar um navegador de mapas no Editor.

Exemplo de árvore:

```text
WORLD
├── Pallet Town
│   ├── Player House 1F
│   ├── Player House 2F
│   ├── Rival House
│   └── Oak Lab
├── Route 1
└── Viridian City
    ├── Pokemon Center
    ├── Mart
    └── Gym
```

Ao selecionar um mapa:

1. carregar o `MapDefinition`;
2. gerar ou localizar o Tilemap correspondente;
3. exibir preview no Editor;
4. mostrar propriedades;
5. listar NPCs;
6. listar warps;
7. listar eventos;
8. permitir abrir a Scene/Prefab gerada.

---

# Roadmap de implementação

## Fase -1 — Preparar o workspace multi-agent do Codex

### Objetivo

Versionar a estratégia de execução do roadmap antes de começar a implementação do jogo/framework.

### Tarefas

- [x] Criar `AGENTS.md` na raiz.
- [x] Criar `.codex/config.toml`.
- [x] Habilitar subagents.
- [x] Configurar limite inicial de concorrência.
- [x] Criar `rrpg_architect`.
- [x] Criar `rom_analyst`.
- [x] Criar `parser_worker`.
- [x] Criar `unity_worker`.
- [x] Criar `test_worker`.
- [x] Criar `docs_worker`.
- [x] Criar `milestone_reviewer`.
- [x] Criar skill `rrpg-orchestrator`.
- [x] Criar skill `plan-current-mvp`.
- [x] Criar skill `analyze-rom-structure`.
- [x] Criar skill `implement-unity-task`.
- [x] Criar skill `validate-milestone`.
- [x] Criar `docs/AI_WORKFLOW.md`.
- [x] Validar que Codex detecta os custom agents.
- [x] Validar que Codex detecta as repo-scoped skills.
- [x] Executar uma tarefa read-only de teste com `rrpg_architect`.
- [x] Executar uma delegação simples para `test_worker` ou outro agent não destrutivo.
- [x] Confirmar que o main thread recebe o resumo do subagent.

### Critério de aceite

A fase termina quando:

```text
Codex IDE
   |
   +--> lê AGENTS.md
   |
   +--> detecta .agents/skills
   |
   +--> detecta .codex/agents
   |
   +--> consegue delegar para pelo menos 2 agents
   |
   +--> cada agent usa o papel/modelo configurado
   |
   +--> main thread consolida os resultados
```

Nenhum código de parsing de ROM deve ser implementado nesta fase.

---

## Fase 0 — Bootstrap do projeto

### Objetivo

Criar a base técnica sem implementar parsing específico ainda.

### Tarefas

- [x] Criar projeto Unity 6.5 2D.
- [x] Configurar Git.
- [x] Criar `.gitignore` adequado para Unity.
- [x] Criar assembly definitions por camada.
- [x] Criar estrutura de diretórios.
- [x] Criar namespaces.
- [x] Criar `RetroRPG.Core`.
- [x] Criar `RetroRPG.IR`.
- [x] Criar `RetroRPG.Editor`.
- [x] Criar `RetroRPG.Runtime`.
- [x] Criar `RetroRPG.Importers.GBA`.
- [x] Criar pasta de testes.
- [x] Criar documentação inicial.

### Critério de aceite

O projeto deve abrir e compilar sem warnings críticos, com assemblies separados e sem dependências circulares.

---

# MVP 0 — ROM Inspector

## Objetivo

Selecionar uma ROM `.gba`, ler seu conteúdo e identificar o jogo.

Fluxo:

```text
Selecionar .gba
    ↓
Ler bytes
    ↓
Parsear header
    ↓
Identificar ROM
    ↓
Mostrar informações
```

### Tarefas

- [x] Implementar `RomFile`.
- [x] Implementar leitura binária segura.
- [x] Implementar `GbaHeaderParser`.
- [x] Extrair título interno da ROM.
- [x] Extrair game code.
- [x] Extrair maker code.
- [x] Validar tamanho mínimo.
- [x] Criar enum/identificador de plataforma.
- [x] Criar `GameDetector`.
- [x] Criar suporte inicial a Pokémon FireRed.
- [x] Criar `ROM Inspector Window`.
- [x] Mostrar dados do header no Editor.
- [x] Mostrar tamanho da ROM.
- [x] Mostrar hash do arquivo para diagnóstico.
- [x] Criar logs de detecção.
- [x] Criar testes para ROM header parser.

### Critério de aceite

Ao selecionar uma ROM suportada de FireRed, a ferramenta deve identificar o jogo e exibir seus dados sem iniciar qualquer importação de mapa.

---

# MVP 1 — Pallet Town renderizada

## Objetivo

Extrair os dados necessários para reconstruir **Pallet Town** e gerar um Tilemap Unity visível.

Fluxo:

```text
FireRed.gba
    ↓
Extrair tiles
    ↓
Extrair paleta
    ↓
Extrair mapa Pallet Town
    ↓
Gerar sprites/tiles
    ↓
Gerar Tilemap Unity
    ↓
Abrir Scene
```

### Tarefas

#### Parsing

- [x] Criar abstração `RomReader`.
- [x] Implementar leitura little-endian.
- [x] Implementar leitura de ponteiros GBA quando necessária.
- [x] Criar `FireRedRomLayout`.
- [x] Centralizar offsets/endereços conhecidos.
- [x] Implementar parser do mapa alvo.
- [x] Implementar parser de tileset usado pelo mapa.
- [x] Implementar parser de paleta usada pelo tileset.
- [x] Converter tiles para representação intermediária.

#### IR

- [x] Criar `MapDefinition`.
- [x] Criar `TilesetDefinition`.
- [x] Criar `PaletteDefinition`.
- [x] Criar identificadores estáveis.
- [x] Permitir serialização de debug para JSON.

#### Unity

- [x] Converter tile gráfico em `Texture2D`.
- [x] Gerar sprites.
- [x] Gerar `Tile` assets.
- [x] Criar `Grid`.
- [x] Criar `Tilemap`.
- [x] Popular Tilemap usando `MapDefinition`.
- [x] Salvar assets gerados em `Assets/Imported/FireRed`.
- [x] Criar Scene de preview.

#### Editor

- [x] Adicionar botão `Import Pallet Town`.
- [x] Mostrar progresso por etapa.
- [x] Mostrar quantidade de tiles processados.
- [x] Mostrar caminho dos assets gerados.

### Critério de aceite

Ao pressionar Play ou abrir a Scene gerada, **Pallet Town deve aparecer corretamente renderizada**, ainda sem personagem jogável.

---

# MVP 2 — Personagem, grid e colisão

## Objetivo

Permitir caminhar por Pallet Town.

### Tarefas

- [x] Criar `PlayerController`.
- [x] Implementar movimento em grid.
- [x] Configurar velocidade parametrizável.
- [x] Criar suporte a direção.
- [x] Criar animação idle.
- [x] Criar animação walking.
- [x] Extrair/importar sprite necessário do personagem, quando suportado.
- [x] Criar camada de colisão.
- [x] Gerar colisões a partir dos dados disponíveis.
- [x] Impedir movimento para células bloqueadas.
- [x] Adicionar câmera seguindo jogador.
- [x] Garantir pixel-perfect no renderer clássico.

### Critério de aceite

O jogador deve conseguir andar por Pallet Town sem atravessar tiles marcados como bloqueados.

---

# MVP 3 — Warps e interiores

## Objetivo

Permitir transições entre Pallet Town e seus interiores.

Fluxo mínimo:

```text
Pallet Town
    ↓
Player House 1F
    ↓
Player House 2F
    ↓
Player House 1F
    ↓
Pallet Town
```

### Tarefas

- [ ] Implementar `WarpDefinition`.
- [ ] Parsear warps do mapa.
- [ ] Criar `MapTransitionSystem`.
- [ ] Carregar mapa alvo.
- [ ] Posicionar jogador no destino.
- [ ] Evitar loops de warp ao spawnar.
- [ ] Adicionar fade opcional de transição.
- [ ] Importar Player House 1F.
- [ ] Importar Player House 2F.
- [ ] Importar pelo menos um segundo interior.
- [ ] Testar retorno para Pallet Town.

### Critério de aceite

O jogador deve entrar e sair da Player House e navegar entre os dois andares sem reposicionamentos incorretos.

---

# MVP 4 — NPCs

## Objetivo

Importar e instanciar NPCs básicos.

Estrutura mínima:

```text
NPC
 ↓
sprite
 ↓
posicao
 ↓
orientacao
 ↓
movimento
 ↓
evento
```

### Tarefas

- [ ] Implementar `NpcDefinition`.
- [ ] Parsear NPCs do mapa.
- [ ] Importar sprites necessários.
- [ ] Criar `NpcController`.
- [ ] Configurar posição inicial.
- [ ] Configurar direção inicial.
- [ ] Implementar NPC parado.
- [ ] Implementar NPC que olha em direção ao jogador.
- [ ] Preparar abstração para padrões de movimento.
- [ ] Associar NPC a `eventId`.

### Critério de aceite

Pallet Town deve apresentar NPCs nas posições corretas, com sprites e direção coerentes.

---

# MVP 5 — Diálogos e interação

## Objetivo

Permitir interação jogador → NPC → diálogo.

### Tarefas

- [ ] Implementar `DialogueDefinition`.
- [ ] Implementar parser inicial de texto/evento necessário.
- [ ] Criar `InteractionSystem`.
- [ ] Detectar objeto à frente do jogador.
- [ ] Criar UI de diálogo.
- [ ] Exibir texto progressivamente.
- [ ] Permitir avançar páginas.
- [ ] Bloquear movimento durante diálogo.
- [ ] Finalizar diálogo corretamente.
- [ ] Permitir NPC virar em direção ao jogador.

### Critério de aceite

O jogador deve aproximar-se de pelo menos um NPC em Pallet Town, interagir e visualizar um diálogo funcional.

---

# MVP 6 — Grama e encontros

## Objetivo

Implementar zonas de encontro aleatório.

### Tarefas

- [ ] Criar `EncounterZoneDefinition`.
- [ ] Criar `EncounterTableDefinition`.
- [ ] Identificar tiles/zones que permitem encontros.
- [ ] Detectar passos em zona válida.
- [ ] Implementar chance de encontro.
- [ ] Criar evento `EncounterTriggered`.
- [ ] Selecionar criatura a partir de uma tabela.
- [ ] Exibir tela temporária de debug antes do BattleSystem final.

### Critério de aceite

Ao caminhar em uma zona de encontro configurada, o sistema deve eventualmente disparar um encontro válido e informar qual criatura foi selecionada.

---

# MVP 7 — Sistema de batalha

## Objetivo

Criar a primeira batalha jogável usando dados importados.

### Tarefas

- [ ] Implementar `CreatureDefinition`.
- [ ] Implementar `SkillDefinition`.
- [ ] Implementar stats básicos.
- [ ] Importar sprites front/back quando suportados.
- [ ] Criar `BattleState`.
- [ ] Criar turn loop.
- [ ] Criar seleção de ações.
- [ ] Criar ataque básico.
- [ ] Criar cálculo de dano inicial.
- [ ] Criar HP.
- [ ] Criar vitória/derrota.
- [ ] Retornar ao mapa após batalha.
- [ ] Persistir estado mínimo do jogador.

### Critério de aceite

Um encontro deve poder abrir uma batalha, permitir ao jogador escolher uma ação, resolver turnos e retornar ao mapa ao final.

---

# Fase 8 — Importação genérica de mapas

## Objetivo

Remover dependência manual de Pallet Town e permitir importar múltiplos mapas pela mesma pipeline.

### Tarefas

- [ ] Criar catálogo de mapas.
- [ ] Importar mapas por identificador.
- [ ] Importar todos os mapas selecionados.
- [ ] Criar cache de tilesets compartilhados.
- [ ] Evitar duplicação de sprites.
- [ ] Evitar duplicação de palettes.
- [ ] Criar referências estáveis entre mapas.
- [ ] Criar Map Browser completo.
- [ ] Suportar reimport incremental.
- [ ] Produzir relatório de importação.

### Critério de aceite

O usuário deve selecionar vários mapas no Editor e importá-los sem alterar código-fonte.

---

# Fase 9 — Dados de jogo em ScriptableObjects

## Objetivo

Converter dados do IR para assets editáveis na Unity.

Exemplo:

```csharp
[CreateAssetMenu(menuName = "RetroRPG/Creature")]
public class CreatureData : ScriptableObject
{
    public string creatureName;
    public int hp;
    public int attack;
    public int defense;
    public int speed;
    public Sprite frontSprite;
    public Sprite backSprite;
}
```

### Tarefas

- [ ] Criar `CreatureData`.
- [ ] Criar `SkillData`.
- [ ] Criar `ItemData`.
- [ ] Criar `TrainerData` quando necessário.
- [ ] Criar `MapData` quando fizer sentido.
- [ ] Garantir reimport sem duplicação.
- [ ] Preservar IDs estáveis.
- [ ] Separar dado importado de overrides manuais futuros.

---

# Fase 10 — Renderer clássico

## Objetivo

Ter uma camada visual fiel ao estilo GBA.

### Requisitos

- Pixel-perfect.
- Tiles 2D.
- Sprites 2D.
- Câmera ortográfica.
- Movimento em grid.
- Sorting consistente.
- Paletas preservadas quando possível.

### Critério de aceite

O runtime clássico deve funcionar sem depender de recursos HD-2D.

---

# Fase 11 — Renderer HD-2D

## Objetivo

Criar uma segunda camada visual sem alterar o `RetroRPG.Core`.

Arquitetura desejada:

```text
             RetroRPG Core
                  |
          +-------+-------+
          |               |
          v               v
 Classic Renderer     HD-2D Renderer
          |               |
 sprites GBA           sprites + 3D
 tilemaps              luz dinamica
 pixel perfect         sombras
                       particulas
                       agua
                       vegetacao
```

### Regra arquitetural

A troca de renderer não deve alterar regras de gameplay, mapas, diálogos, encontros ou batalha.

### Tarefas futuras

- [ ] Criar interface/abstração de apresentação de mapa.
- [ ] Criar cenário 2.5D.
- [ ] Adicionar iluminação dinâmica.
- [ ] Adicionar sombras.
- [ ] Adicionar partículas.
- [ ] Adicionar água.
- [ ] Adicionar vegetação.
- [ ] Manter sprites 2D quando desejado.

---

# Fase 12 — Segundo importador: Medabots

## Objetivo

Usar Medabots como teste real da arquitetura genérica.

O sucesso desta fase prova que o framework não virou um `PokemonEngine` disfarçado.

### Tarefas

- [ ] Criar `Importers/GBA/Medabots`.
- [ ] Reutilizar `RomReader`.
- [ ] Reutilizar IR genérico quando aplicável.
- [ ] Criar extensões do IR apenas quando realmente necessárias.
- [ ] Importar pelo menos um mapa.
- [ ] Importar pelo menos um NPC.
- [ ] Importar pelo menos um conjunto de dados de batalha.
- [ ] Documentar incompatibilidades conceituais.
- [ ] Evitar condicionais do tipo `if (game == Pokemon)` no Core.

### Critério de aceite

O mesmo pipeline deve conseguir gerar conteúdo Unity a partir de um jogo não-Pokémon sem alterações estruturais no Core.

---

# Fase futura — Nintendo DS / Digimon

Somente iniciar após a arquitetura GBA estar estável.

Estrutura prevista:

```text
RetroRPG.Importers/
└── NDS/
    ├── Common/
    └── Digimon/
```

Possíveis novos componentes:

```text
NDS filesystem parser
NARC archive parser
Texture parser
Sprite parser
Model parser
Map parser
Script parser
```

Não implementar nesta etapa inicial.

---

# Testes

## EditMode

Prioridade alta.

Cobrir:

- ROM header parsing.
- Bounds checking.
- Pointer conversion.
- Palette conversion.
- Tile decoding.
- Map decoding.
- IR serialization.
- Game detection.
- Reimport logic.

## PlayMode

Cobrir progressivamente:

- movimento;
- colisão;
- warp;
- interação;
- diálogo;
- encontro;
- batalha.

---

# Logging e diagnóstico

Toda importação deve registrar:

```text
ROM
Game detected
Stage
Input offset/range
Output asset
Warnings
Errors
Elapsed time
```

Categorias sugeridas:

```text
ROM
HEADER
GAME_DETECTION
PALETTE
TILESET
MAP
SPRITE
NPC
EVENT
DIALOGUE
WARP
UNITY_IMPORT
```

Erros de parsing devem conter contexto suficiente para reprodução.

Exemplo:

```text
[MAP] Failed to decode map 'PalletTown'
ROM offset: 0xXXXXXXXX
Expected width: XX
Expected height: YY
Reason: pointer outside ROM bounds
```

---

# Definition of Done global

Uma tarefa é considerada concluída quando:

- [x] Código compila.
- [x] Não cria dependência arquitetural indevida.
- [x] Possui tratamento de erro adequado.
- [x] Possui log útil quando aplicável.
- [x] Possui teste quando a unidade é testável.
- [x] Não introduz ROM/assets proprietários no Git.
- [x] Não quebra importações já implementadas.
- [x] Documentação relevante foi atualizada.

---

# Prioridade imediata para o Codex

Executar apenas nesta ordem:

```text
0. Workspace Codex: AGENTS.md + custom agents + skills + orchestrator
1. Bootstrap do projeto
2. ROM Inspector
3. Identificação de FireRed
4. Parser mínimo de Pallet Town
5. Tileset + paleta
6. IR de mapa
7. Tilemap Unity
8. Preview de Pallet Town
9. Personagem + grid
10. Colisão
11. Warps/interiores
12. NPCs
13. Diálogos
14. Encontros
15. Batalha
16. Importação genérica de mapas
17. Medabots
18. HD-2D
19. NDS/Digimon
```

Não antecipar fases posteriores se o critério de aceite da fase atual ainda não estiver atendido.

---

# Primeiro comando de trabalho para o Codex

## Passo 1 — configurar a equipe de agents

Primeiro prompt recomendado no Codex IDE:

> Leia `ROADMAP_CODEX_UPDATED.md`. Execute somente a Fase -1. Crie o `AGENTS.md`, a configuração multi-agent, os custom agents e as repo-scoped skills descritas no roadmap. Não implemente ainda nenhuma funcionalidade de ROM ou Unity. Valide que agents e skills estão detectáveis no Codex e documente o workflow em `docs/AI_WORKFLOW.md`.

## Passo 2 — iniciar o framework

Depois que a Fase -1 estiver validada:

> Use `$rrpg-orchestrator` para iniciar a Fase 0 e o primeiro MVP técnico. Peça ao `rrpg_architect` para planejar a entrega antes de qualquer edição. Em seguida delegue implementação para `parser_worker`/`unity_worker` conforme a tarefa, testes para `test_worker`, documentação para `docs_worker` e review final para `milestone_reviewer`. Criar a fundação do `Retro RPG Reconstruction Framework` em Unity 6.5 + C#, com assemblies separados para Core, IR, Runtime, Editor e Importers. Implementar uma janela de Editor capaz de selecionar uma ROM `.gba`, ler seu header com segurança, calcular um hash de diagnóstico e identificar Pokémon FireRed quando a ROM corresponder ao adaptador suportado. Não implementar mapas, sprites, batalha ou gameplay nesta primeira entrega.

### Saída esperada da primeira entrega funcional

- workspace multi-agent validado;
- projeto compilando;
- estrutura de diretórios criada;
- assembly definitions configuradas;
- `RomFile`;
- `RomReader`;
- `GbaHeaderParser`;
- `GameDetector`;
- adaptador inicial `PokemonFireRed`;
- `RomInspectorWindow`;
- testes EditMode do parser;
- `docs/AI_WORKFLOW.md`;
- `docs/ARCHITECTURE.md`;
- `docs/ROM_FORMAT.md`;
- README com instruções para abrir o projeto e testar o inspector;
- review do `milestone_reviewer` sem finding bloqueante.

---

# Visão final

```text
FireRed ROM -----+
                  |
Emerald ROM ------+----> RetroRPG IR ----> Unity
                  |
Medabots ROM -----+

                           |
                   +-------+-------+
                   |               |
                   v               v
              Classic 2D        HD-2D
```

O primeiro marco real do projeto não é “portar Pokémon FireRed”.

O primeiro marco real é:

> **Selecionar uma ROM local de FireRed e gerar Pallet Town dentro da Unity através de uma pipeline reproduzível, desacoplada e extensível.**
