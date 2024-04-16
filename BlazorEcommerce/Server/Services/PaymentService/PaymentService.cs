using Microsoft.OpenApi.Validations;
using Stripe;
using Stripe.Checkout;

namespace BlazorEcommerce.Server.Services.PaymentService;

public class PaymentService : IPaymentService
{
    private readonly ICartService _cartService;
    private readonly IAuthService _authService;
    private readonly IOrderService _orderService;

    const string secret = "whsec_bd77e2335defcaaca023b3ae4c6a3328fa86d3d0b84bc81a0fde5c264e417a6c";

    public PaymentService(ICartService cartService,IAuthService authService, IOrderService orderService)
    {
        StripeConfiguration.ApiKey = "sk_test_51P50nTRxI1kZrOCwxPXClJU116Q39DhiclkYbKnI3qifEx9az9A5O9FZMmdNxkIxt4KLwncpV5q7g87gSfAKHvgW00tuNneP6l";
        _cartService = cartService;
        _authService = authService;
        _orderService = orderService;
    }

    public async Task<Session> CreateCheckoutSession()
    {
        var products = (await _cartService.GetDbCartProducts(null)).Data;
        var lineItems = new List<SessionLineItemOptions>();
        products.ForEach(product => lineItems.Add(new SessionLineItemOptions
        {
            PriceData = new SessionLineItemPriceDataOptions
            {
                UnitAmountDecimal = product.Price * 100,
                Currency = "usd",
                ProductData = new SessionLineItemPriceDataProductDataOptions
                {
                    Name = product.Title,
                    Images = new List<string> { product.ImageUrl }
                }
            },
            Quantity = product.Quantity,
        }));

        var options = new SessionCreateOptions
        {
            CustomerEmail = _authService.GetUserEmail(),
            PaymentMethodTypes = new List<string>() { "card"},
            LineItems = lineItems,
            Mode = "payment",
            SuccessUrl = "https://localhost:7164/order-success",
            CancelUrl = "https://localhost:7164/cart"

        };

        var service = new SessionService();
        Session session = await service.CreateAsync(options);
        return session;
    }
    /// <summary>
    /// enter the command to stripe cli
    /// stripe listen --forward-to https://localhost:7164/api/payment
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    public async Task<ServiceResponse<bool>> FulfillOrder(HttpRequest request)
    {
        var json = await new StreamReader(request.Body).ReadToEndAsync();
        try
        {
            var stripeEvent = EventUtility.ConstructEvent(json,
                request.Headers["Stripe-Signature"],
                secret);

            if(stripeEvent.Type == Events.CheckoutSessionCompleted)
            {
                var session = stripeEvent.Data.Object as Session;
                var user = await _authService.GetUserByEmail(session.CustomerEmail);
                await _orderService.PlaceOrder(user.Id);
            }
            return new ServiceResponse<bool> { Data = true };
        }
        catch (StripeException e)
        {
            return new ServiceResponse<bool> { Data = false,Success=false,Message = e.Message };
        }
    }
}
