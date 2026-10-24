using Verse;
using Xunit;

namespace TotalFog.Tests;

public class HearingCueGraphicTests
{
    [Theory]
    [InlineData(-1, 3501)]
    [InlineData(3000, 3501)]
    [InlineData(4000, 4000)]
    public void Hearing_material_draws_after_fog_without_lowering_an_explicit_queue(
        int requested,
        int expected
    )
    {
        var graphic = new Graphic_HearingCue();
        int originalFogQueue = MatBases.FogOfWar.renderQueue;

        graphic.Init(new GraphicRequest { renderQueue = requested });

        Assert.Equal(expected, graphic.AppliedRequest.renderQueue);
        Assert.True(graphic.AppliedRequest.renderQueue > MatBases.FogOfWar.renderQueue);
        Assert.Equal(originalFogQueue, MatBases.FogOfWar.renderQueue);
    }
}
