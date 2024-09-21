// © SS220, An EULA/CLA with a hosting restriction, full text: https://raw.githubusercontent.com/SerbiaStrong-220/space-station-14/master/CLA.txt

using Content.Shared.Verbs;
using Content.Shared.SS220.Cart.Components;
using Content.Shared.DragDrop;
using Content.Shared.Interaction;
using Content.Shared.Movement.Pulling.Components;

namespace Content.Shared.SS220.Cart;

public sealed partial class CartPullerSystem : EntitySystem
{
    [Dependency] private readonly CartSystem _cart = default!;
    //[Dependency] private readonly SharedInteractionSystem _interaction = default!; Used for drag&drop

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CartPullerComponent, GetVerbsEvent<InteractionVerb>>(AddCartVerbs);
        //SubscribeLocalEvent<CartPullerComponent, CanDropTargetEvent>(OnCanDrop);
        //SubscribeLocalEvent<CartPullerComponent, DragDropTargetEvent>(OnDragDropTarget);
        SubscribeLocalEvent<CartPullerComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<CartPullerComponent, CartAttachEvent>(OnAttachCart);
        SubscribeLocalEvent<CartPullerComponent, CartDeattachEvent>(OnDeattachCart);
    }

    private void OnShutdown(Entity<CartPullerComponent> entity, ref ComponentShutdown args)
    {
        if (entity.Comp.AttachedCart is not { } attachedCart)
            return;

        if (!TryComp<CartComponent>(attachedCart, out var cartComp))
            return;

        _cart.TryDeattachCart(entity, (attachedCart, cartComp), null);
    }

    //private void OnCanDrop(EntityUid uid, CartPullerComponent component, ref CanDropTargetEvent args)
    //{
    //    args.CanDrop = CartCanDragDropOn(uid, args.User, uid, args.Dragged, component);
    //    args.Handled = true;
    //}

    //private void OnDragDropTarget(EntityUid uid, CartPullerComponent component, ref DragDropTargetEvent args)
    //{
    //    // Cart drag-drop attaching
    //    if (!CartCanDragDropOn(uid, args.User, uid, args.Dragged, component))
    //        return;

    //    if (!TryComp<CartComponent>(args.Dragged, out var cartComp))
    //        return;

    //    args.Handled = _cart.TryAttachCart(uid, cartComp, args.User);
    //}

    //// Successfully stolen from BuckleSystem lmao
    //private bool CartCanDragDropOn(
    //    EntityUid cartPuller,
    //    EntityUid user,
    //    EntityUid target,
    //    EntityUid cart,
    //    CartPullerComponent? cartPullerComp = null,
    //    CartComponent? cartComp = null)
    //{

    //    if (!Resolve(cartPuller, ref cartPullerComp, false) ||
    //        !Resolve(cart, ref cartComp, false))
    //        return false;

    //    if (cartPullerComp.AttachedCart.HasValue)
    //        return false;

    //    bool Ignored(EntityUid entity) => entity == user || entity == cart || entity == target;

    //    return _interaction.InRangeUnobstructed(target, cart, cartComp.AttachRange, predicate: Ignored);
    //}

    private void AddCartVerbs(Entity<CartPullerComponent> entity, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        var argsInner = args;

        if (entity.Comp.AttachedCart.HasValue)
        {
            var attachedCart = entity.Comp.AttachedCart;
            if (!attachedCart.HasValue)
                return;

            // If cart puller already have an attached cart - add verb to deattach it
            if (!TryComp<CartComponent>(entity.Comp.AttachedCart, out var attachedCartComp))
                return;

            InteractionVerb deattachVerb = new()
            {
                Text = Name(attachedCart.Value),
                Act = () => _cart.TryDeattachCart((attachedCart.Value, attachedCartComp), argsInner.User),
                Category = VerbCategory.DeattachCart
            };
            args.Verbs.Add(deattachVerb);
            return;
        }

        if (!TryComp<PullerComponent>(args.User, out var userPullerComp))
            return;

        var cart = userPullerComp.Pulling;
        if (!TryComp<CartComponent>(cart, out var cartComp))
            return;

        if (!_cart.IsAttachable(entity, cart.Value))
            return;

        InteractionVerb verb = new()
        {
            Text = Name(cart.Value),
            Act = () => _cart.TryAttachCart(entity, cartComp, argsInner.User),
            Category = VerbCategory.AttachCart
        };
        args.Verbs.Add(verb);
    }

    private void OnDeattachCart(EntityUid uid, CartPullerComponent component, ref CartDeattachEvent args)
    {
        component.AttachedCart = null;
        Dirty(uid, component);
    }

    private void OnAttachCart(EntityUid uid, CartPullerComponent component, ref CartAttachEvent args)
    {
        component.AttachedCart = args.Attaching;
        Dirty(uid, component);
    }
}
