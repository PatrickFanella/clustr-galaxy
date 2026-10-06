using System.Numerics;
using ClustrGalaxy.Core.Navigation;
using NUnit.Framework;
namespace ClustrGalaxy.Tests
{
    public sealed class ShipControlTests
    {
        [Test] public void AttitudeInputOnlyTiltsAndRollsNeverThrusts()
        {
            var up=ShipControlInput.FromGamepad(Vector2.UnitY,Vector2.Zero,0,0,0,0);
            Assert.That(up.Translation,Is.EqualTo(Vector3.Zero));Assert.That(up.Rotation.X,Is.EqualTo(-1));
            var roll=ShipControlInput.FromGamepad(Vector2.UnitX,Vector2.Zero,0,0,0,0);
            Assert.That(roll.Translation,Is.EqualTo(Vector3.Zero));Assert.That(roll.Rotation.Z,Is.EqualTo(-1));
        }
        [Test] public void TriggersControlForwardReverseIndependentlyOfAttitude()
        {
            Assert.That(ShipControlInput.FromGamepad(Vector2.Zero,Vector2.Zero,1,0,0,0).Translation.Z,Is.EqualTo(1));
            Assert.That(ShipControlInput.FromGamepad(Vector2.Zero,Vector2.Zero,0,1,0,0).Translation.Z,Is.EqualTo(-1));
            Assert.That(ShipControlInput.FromGamepad(Vector2.Zero,Vector2.Zero,1,1,0,0).Translation.Z,Is.Zero);
        }
        [Test] public void YawLiftInputAndBumpersCoverRemainingAxes()
        {
            var yaw=ShipControlInput.FromGamepad(Vector2.Zero,Vector2.UnitX,0,0,0,0);Assert.That(yaw.Rotation.Y,Is.EqualTo(1));Assert.That(yaw.Translation,Is.EqualTo(Vector3.Zero));
            Assert.That(ShipControlInput.FromGamepad(Vector2.Zero,Vector2.UnitY,0,0,0,0).Translation.Y,Is.EqualTo(1));
            Assert.That(ShipControlInput.FromGamepad(Vector2.Zero,Vector2.Zero,0,0,1,0).Translation.X,Is.EqualTo(1));
        }
        [Test] public void CentrePrecisionIsSofterAndFullInputKeepsFullAuthority()
        {
            Assert.That(ShipControlInput.FromGamepad(new Vector2(.5f,0),Vector2.Zero,0,0,0,0).Rotation.Z,Is.EqualTo(-.25f));
            Assert.That(ShipControlInput.FromGamepad(Vector2.One,Vector2.One,1,0,1,0).Translation.Length(),Is.EqualTo(1).Within(.0001));
        }
    }
}
