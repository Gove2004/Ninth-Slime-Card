using UnityEngine;
using UnityEngine.EventSystems;

namespace Slime.Game
{
    /// <summary>
    /// 拖拽宿主。手牌区把「能不能拖、拖到哪、放下了没有」这三件事交给战斗界面，
    /// 拖拽组件本身只负责把 Unity 的指针事件翻译成人话。
    /// </summary>
    public interface ICardDragHost
    {
        /// <summary>当前是否允许拖动这张牌（不是你的回合、动画播放中、已结束都不能拖）。</summary>
        bool CanDragCard(CardView view);

        void OnCardDragBegin(CardView view, PointerEventData data);

        void OnCardDragMove(CardView view, PointerEventData data);

        void OnCardDragEnd(CardView view, PointerEventData data);

        /// <summary>指针悬停在某张手牌上（进入/离开），用于手牌抬升。</summary>
        void OnCardHover(CardView view, bool entered);

        /// <summary>按下但没有拖动（轻点）——用于提示「要拖动出牌」。</summary>
        void OnCardTapped(CardView view);
    }

    /// <summary>
    /// 挂在卡牌根节点上的指针事件桥。实现拖拽与悬停，把结果转发给 <see cref="ICardDragHost"/>。
    /// </summary>
    public sealed class CardDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler,
        IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public CardView View;
        public ICardDragHost Host;

        /// <summary>本次按下是否已经真正进入了拖动（未拖动则视为轻点）。</summary>
        private bool dragging;

        public bool IsDragging { get { return dragging; } }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!dragging && Host != null && View != null)
            {
                Host.OnCardHover(View, true);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!dragging && Host != null && View != null)
            {
                Host.OnCardHover(View, false);
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (Host == null || View == null || !Host.CanDragCard(View))
            {
                return;
            }

            dragging = true;
            Host.OnCardDragBegin(View, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!dragging || Host == null || View == null)
            {
                return;
            }

            Host.OnCardDragMove(View, eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!dragging)
            {
                return;
            }

            dragging = false;
            if (Host != null && View != null)
            {
                Host.OnCardDragEnd(View, eventData);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // 拖拽结束时 Unity 不会再补发 click，所以这里只可能是「按下即松开」的轻点。
            if (Host != null && View != null)
            {
                Host.OnCardTapped(View);
            }
        }
    }
}
