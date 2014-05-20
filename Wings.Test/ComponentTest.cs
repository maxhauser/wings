using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Wings.Test
{
    public class ComponentTest
    {
        [Fact]
        public async Task TestStart()
        {
            bool called = false;
            var component = Component.Create<bool>(async run =>
            {
                called = true;
                await run(true);
            });
            await component.Start();
            Assert.True(called);
            Assert.Equal(ComponentStateType.Started, component.State.Type);
        }

        [Fact]
        public async Task TestStop()
        {
            bool called = false;
            var component = Component.Create<bool>(async run =>
            {
                await run(true);
                called = true;
            });
            await component.Start();
            Assert.False(called);

            await component.Stop();
            Assert.True(called);
        }
    }
}
