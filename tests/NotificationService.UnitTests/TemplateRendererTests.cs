using Xunit;
using NotificationService.Application;

namespace NotificationService.UnitTests;

public sealed class TemplateRendererTests
{
    [Fact]
    public void Render_ReplacesAllPlaceholders()
    {
        var renderer = new TemplateRenderer();

        var rendered = renderer.Render(
            "Pedido {{orderId}} para {{buyer.name}} está {{status}}.",
            new Dictionary<string, string>
            {
                ["orderId"] = "ORD-123",
                ["buyer.name"] = "Ana",
                ["status"] = "confirmado"
            });

        Assert.Equal("Pedido ORD-123 para Ana está confirmado.", rendered);
    }

    [Fact]
    public void Render_ThrowsWhenRequiredValueIsMissing()
    {
        var renderer = new TemplateRenderer();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            renderer.Render("Olá {{name}}", new Dictionary<string, string>()));

        Assert.Contains("Template value 'name'", exception.Message);
    }
}
