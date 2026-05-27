using UnityEngine;
using UnityEngine.UI;

public class HUD : MonoBehaviour
{
    private Player player;
    [SerializeField] private RollTMP hpText;
    [SerializeField] private RollTMP manaText;

    private Enemy enemy;
    [SerializeField] private RollTMP enemyHpText;
    [SerializeField] private Image enemyHpBar;

    private void Update()
    {
        if (BattleManager.Instance == null) return;

        if (player == null)
        {
            player = BattleManager.Instance.Player;
        }
        if (enemy == null)
        {
            enemy = BattleManager.Instance.Enemy;
        }

        if (player != null)
        {
            hpText.StopAndSetFinalValue("{0}", (int)player.Attributes.GetValue(StaticString.属性.生命));
            manaText.StopAndSetFinalValue("{0}", (int)player.Attributes.GetValue(StaticString.属性.法力));
        }

        if (enemy != null)
        {
            enemyHpText.StopAndSetFinalValue("{0}", (int)enemy.Attributes.GetValue(StaticString.属性.生命));
            enemyHpBar.fillAmount = enemy.HealthPercent;
        }
    }
}
