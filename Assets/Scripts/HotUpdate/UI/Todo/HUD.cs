// using TMPro;
// using UnityEngine;
// using UnityEngine.UI;
// using DG.Tweening;

// public class HUD : MonoBehaviour
// {
//     // UI元素
//     public TextMeshProUGUI playerHPText;
//     public TextMeshProUGUI playerSPText;
//     public TextMeshProUGUI playerMPText;
//     public Image playerHPBgImage;
//     public Image playerMPBgImage; // 新增：MP背景图片引用
//     public TextMeshProUGUI enemyHPText;
//     public TextMeshProUGUI enemySPText;
//     public TextMeshProUGUI enemyMPText;
//     public Image enemyHPBgImage;

//     private BaseCharacter lastPlayerInstance;
//     private RectTransform enemyHPBgRect;
//     private Sprite nonEndlessEnemyHpSprite;
//     private Sprite originalEnemyHpSprite;
//     private float originalEnemyHpBgWidth;
//     private bool hasOriginalEnemyHpBgState = false;
//     private bool hasAppliedNonEndlessEnemyHpStyle = false;
//     private float originalEnemyHpFontSize;
//     private Color originalEnemyHpColor;
//     private TMP_FontAsset originalEnemyHpFont;
//     private FontStyles originalEnemyHpFontStyle;
//     private Material originalEnemyHpFontMaterial;
//     private bool originalEnemyHpEnableVertexGradient;
//     private VertexGradient originalEnemyHpVertexGradient;
//     private TMP_ColorGradient originalEnemyHpColorGradientPreset;
//     private bool hasOriginalEnemyHpTextStyle = false;
//     private BaseCharacter lastEnemyInstance;
//     private ulong lastPlayerHealth;
//     private bool hasLastPlayerHealth;
//     private ulong lastEnemyHealth;
//     private bool hasLastEnemyHealth;
//     private Sequence playerManaPulseSequence;
//     private Sequence playerHpPulseSequence;
//     private Sequence enemyHpPulseSequence;
//     private System.Action onPlayerGainManaUnsub;
//     private bool pendingManaGainEffect;
//     private Vector3 playerManaBaseScale = Vector3.one;
//     private bool hasPlayerManaBaseScale;
//     private Color playerManaBaseColor = Color.white;
//     private bool hasPlayerManaBaseColor;
//     private Vector3 playerHpBaseScale = Vector3.one;
//     private bool hasPlayerHpBaseScale;
//     private Vector3 enemyHpBaseScale = Vector3.one;
//     private bool hasEnemyHpBaseScale;

//     void Start()
//     {
//         if (playerHPBgImage == null)
//         {
//             var foundObj = GameObject.Find("PlayerHP_BG");
//             if (foundObj != null)
//             {
//                 playerHPBgImage = foundObj.GetComponent<Image>();
//             }
//         }
//         // 尝试自动查找 MP 背景
//         if (playerMPBgImage == null)
//         {
//             // 假设 PlayerMP_BG 是一个名为 "PlayerMP_BG" 的子对象或者在场景中唯一
//             var foundObj = GameObject.Find("PlayerMP_BG");
//             if (foundObj != null)
//             {
//                 playerMPBgImage = foundObj.GetComponent<Image>();
//             }
//         }
//         if (playerMPBgImage != null && !hasPlayerManaBaseColor)
//         {
//             playerManaBaseColor = playerMPBgImage.color;
//             hasPlayerManaBaseColor = true;
//         }
//         if (enemyHPBgImage == null)
//         {
//             var foundObj = GameObject.Find("EnemyHP_BG");
//             if (foundObj != null)
//             {
//                 enemyHPBgImage = foundObj.GetComponent<Image>();
//             }
//         }
//         if (enemyHPBgImage != null)
//         {
//             enemyHPBgRect = enemyHPBgImage.rectTransform;
//             if (!hasOriginalEnemyHpBgState)
//             {
//                 originalEnemyHpSprite = enemyHPBgImage.sprite;
//                 originalEnemyHpBgWidth = enemyHPBgRect != null ? enemyHPBgRect.sizeDelta.x : 0f;
//                 hasOriginalEnemyHpBgState = true;
//             }
//         }
//         if (enemyHPText != null && !hasOriginalEnemyHpTextStyle)
//         {
//             originalEnemyHpFontSize = enemyHPText.fontSize;
//             originalEnemyHpColor = enemyHPText.color;
//             originalEnemyHpFont = enemyHPText.font;
//             originalEnemyHpFontStyle = enemyHPText.fontStyle;
//             originalEnemyHpFontMaterial = enemyHPText.fontSharedMaterial;
//             originalEnemyHpEnableVertexGradient = enemyHPText.enableVertexGradient;
//             originalEnemyHpVertexGradient = enemyHPText.colorGradient;
//             originalEnemyHpColorGradientPreset = enemyHPText.colorGradientPreset;
//             hasOriginalEnemyHpTextStyle = true;
//         }
//         nonEndlessEnemyHpSprite = LoadBloodSprite();
//         onPlayerGainManaUnsub = EventCenter.Register<CharacterValueEventContext>(GameEvents.PlayerGainedMana, _ =>
//         {
//             pendingManaGainEffect = true;
//         });
//     }

//     void Update()
//     {
//         // 简化逻辑， 每帧更新UI显示
//         if (BattleManager.Instance != null)
//         {
//             var player = BattleManager.Instance.player;
//             var enemy = BattleManager.Instance.enemy;

//             // 检测是否开启了新的一局（玩家实例改变）
//             if (player != lastPlayerInstance)
//             {
//                 lastPlayerInstance = player;
//                 hasLastPlayerHealth = false;
//             }
//             if (enemy != lastEnemyInstance)
//             {
//                 lastEnemyInstance = enemy;
//                 hasLastEnemyHealth = false;
//             }

//             if (player != null)
//             {
//                 playerHPText.text = $"{player.health}";
//                 playerSPText.text = $"SP={player.shiled}";
//                 playerMPText.text = $"{player.mana}";

//                 if (hasLastPlayerHealth && player.health != lastPlayerHealth)
//                 {
//                     PlayHealthImageEffect(playerHPBgImage, ref playerHpPulseSequence, ref hasPlayerHpBaseScale, ref playerHpBaseScale);
//                 }
//                 lastPlayerHealth = player.health;
//                 hasLastPlayerHealth = true;

//                 if (pendingManaGainEffect)
//                 {
//                     if (TryResolvePlayerManaBgImage())
//                     {
//                         PlayManaGainEffect();
//                         pendingManaGainEffect = false;
//                     }
//                 }
//             }
//             if (enemy != null)
//             {
//                 EnemyBoss enemyBoss = enemy as EnemyBoss;
//                 bool isEndless = enemyBoss != null && enemyBoss.IsEndlessMode;
//                 if (isEndless)
//                 {
//                     RestoreEnemyHpStyle();
//                     enemyHPText.text = FormatEnemyHpWithShield(enemyBoss.score, enemy.shiled);
//                 }
//                 else
//                 {
//                     ApplyEnemyHpStyleForNonEndless();
//                     enemyHPText.text = FormatEnemyHpWithShield(enemy.health, enemy.shiled);
//                 }
//                 enemySPText.text = $"SP={enemy.shiled}";
//                 enemyMPText.text = $"MP={enemy.mana}";

//                 if (hasLastEnemyHealth && enemy.health != lastEnemyHealth)
//                 {
//                     PlayHealthImageEffect(enemyHPBgImage, ref enemyHpPulseSequence, ref hasEnemyHpBaseScale, ref enemyHpBaseScale);
//                 }
//                 lastEnemyHealth = enemy.health;
//                 hasLastEnemyHealth = true;
//             }
//         }
//     }

//     private void PlayManaGainEffect()
//     {
//         if (playerMPBgImage == null) return;
//         if (!hasPlayerManaBaseScale)
//         {
//             playerManaBaseScale = playerMPBgImage.transform.localScale;
//             hasPlayerManaBaseScale = true;
//         }
//         if (!hasPlayerManaBaseColor)
//         {
//             playerManaBaseColor = playerMPBgImage.color;
//             hasPlayerManaBaseColor = true;
//         }

//         StopPulse(ref playerManaPulseSequence);
//         playerMPBgImage.transform.localScale = playerManaBaseScale;
//         playerMPBgImage.color = playerManaBaseColor;

//         playerManaPulseSequence = DOTween.Sequence();
//         playerManaPulseSequence.Append(playerMPBgImage.transform.DOScale(playerManaBaseScale * 1.2f, 0.1f).SetEase(Ease.OutQuad));
//         playerManaPulseSequence.Join(playerMPBgImage.DOColor(Color.cyan, 0.1f).SetEase(Ease.OutQuad));
//         playerManaPulseSequence.Append(playerMPBgImage.transform.DOScale(playerManaBaseScale, 0.14f).SetEase(Ease.InOutQuad));
//         playerManaPulseSequence.Join(playerMPBgImage.DOColor(playerManaBaseColor, 0.14f).SetEase(Ease.InOutQuad));
//         playerManaPulseSequence.OnKill(() =>
//         {
//             if (playerMPBgImage == null) return;
//             playerMPBgImage.transform.localScale = playerManaBaseScale;
//             playerMPBgImage.color = playerManaBaseColor;
//         });
//         playerManaPulseSequence.OnComplete(() => playerManaPulseSequence = null);
//     }

//     private bool TryResolvePlayerManaBgImage()
//     {
//         if (playerMPBgImage != null) return true;
//         var foundObj = GameObject.Find("PlayerMP_BG");
//         if (foundObj != null)
//         {
//             playerMPBgImage = foundObj.GetComponent<Image>();
//         }
//         return playerMPBgImage != null;
//     }

//     private void PlayHealthImageEffect(Image hpImage, ref Sequence pulseSequence, ref bool hasBaseScale, ref Vector3 baseScale)
//     {
//         if (hpImage == null) return;
//         if (!hasBaseScale)
//         {
//             baseScale = hpImage.transform.localScale;
//             hasBaseScale = true;
//         }

//         Vector3 defaultScale = baseScale;
//         StopPulse(ref pulseSequence);
//         hpImage.transform.localScale = defaultScale;

//         pulseSequence = DOTween.Sequence();
//         pulseSequence.Append(hpImage.transform.DOScale(defaultScale * 1.2f, 0.1f).SetEase(Ease.OutQuad));
//         pulseSequence.Append(hpImage.transform.DOScale(defaultScale, 0.14f).SetEase(Ease.InOutQuad));
//         pulseSequence.OnKill(() =>
//         {
//             if (hpImage == null) return;
//             hpImage.transform.localScale = defaultScale;
//         });
//     }

//     private static void StopPulse(ref Sequence sequence)
//     {
//         if (sequence == null) return;
//         sequence.Kill(false);
//         sequence = null;
//     }

//     private static string FormatEnemyHpWithShield(ulong hpValue, ulong shieldValue)
//     {
//         if (shieldValue == 0) return hpValue.ToString();
//         return $"<color=#9AA0AA><size=70%>{shieldValue}+</size></color> {hpValue}";
//     }

//     private void OnDestroy()
//     {
//         onPlayerGainManaUnsub?.Invoke();
//         onPlayerGainManaUnsub = null;
//         StopPulse(ref playerManaPulseSequence);
//         StopPulse(ref playerHpPulseSequence);
//         StopPulse(ref enemyHpPulseSequence);
//     }

//     private Sprite LoadBloodSprite()
//     {
//         if (nonEndlessEnemyHpSprite != null) return nonEndlessEnemyHpSprite;
//         var sprites = Resources.LoadAll<Sprite>("UI/blood");
//         for (int i = 0; i < sprites.Length; i++)
//         {
//             if (sprites[i] != null && sprites[i].name == "blood_0")
//             {
//                 nonEndlessEnemyHpSprite = sprites[i];
//                 break;
//             }
//         }
//         return nonEndlessEnemyHpSprite;
//     }

//     private void ApplyEnemyHpStyleForNonEndless()
//     {
//         if (enemyHPText != null && playerHPText != null)
//         {
//             enemyHPText.font = playerHPText.font;
//             enemyHPText.fontSize = playerHPText.fontSize;
//             enemyHPText.color = playerHPText.color;
//             enemyHPText.fontStyle = playerHPText.fontStyle;
//             enemyHPText.fontSharedMaterial = playerHPText.fontSharedMaterial;
//             enemyHPText.enableVertexGradient = playerHPText.enableVertexGradient;
//             enemyHPText.colorGradient = playerHPText.colorGradient;
//             enemyHPText.colorGradientPreset = playerHPText.colorGradientPreset;
//         }
//         if (enemyHPBgImage != null)
//         {
//             if (hasOriginalEnemyHpBgState && enemyHPBgRect != null)
//             {
//                 var size = enemyHPBgRect.sizeDelta;
//                 if (!Mathf.Approximately(size.x, 60f))
//                 {
//                     size.x = 60f;
//                     enemyHPBgRect.sizeDelta = size;
//                 }
//             }
//             var sprite = LoadBloodSprite();
//             if (sprite != null && enemyHPBgImage.sprite != sprite)
//             {
//                 enemyHPBgImage.sprite = sprite;
//             }
//         }
//         hasAppliedNonEndlessEnemyHpStyle = true;
//     }

//     private void RestoreEnemyHpStyle()
//     {
//         if (!hasAppliedNonEndlessEnemyHpStyle) return;
//         if (enemyHPText != null && hasOriginalEnemyHpTextStyle)
//         {
//             enemyHPText.font = originalEnemyHpFont;
//             enemyHPText.fontSize = originalEnemyHpFontSize;
//             enemyHPText.color = originalEnemyHpColor;
//             enemyHPText.fontStyle = originalEnemyHpFontStyle;
//             enemyHPText.fontSharedMaterial = originalEnemyHpFontMaterial;
//             enemyHPText.enableVertexGradient = originalEnemyHpEnableVertexGradient;
//             enemyHPText.colorGradient = originalEnemyHpVertexGradient;
//             enemyHPText.colorGradientPreset = originalEnemyHpColorGradientPreset;
//         }
//         if (enemyHPBgImage != null && hasOriginalEnemyHpBgState)
//         {
//             enemyHPBgImage.sprite = originalEnemyHpSprite;
//             if (enemyHPBgRect != null)
//             {
//                 var size = enemyHPBgRect.sizeDelta;
//                 size.x = originalEnemyHpBgWidth;
//                 enemyHPBgRect.sizeDelta = size;
//             }
//         }
//         hasAppliedNonEndlessEnemyHpStyle = false;
//     }
// }
