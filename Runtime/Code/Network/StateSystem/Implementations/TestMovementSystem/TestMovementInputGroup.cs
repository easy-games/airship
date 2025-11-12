using Code.Network.StateSystem.Structures;
using Mirror;

namespace Code.Network.StateSystem.Implementations.TestMovementSystem {
    public class TestMovementInputGroup : InputCommandGroup<TestMovementInput, TestMovementInputDiff> {
        public TestMovementInputGroup(TestMovementInput[] inputs) : base(inputs) {}
    }

    public static class TestMovementInputGroupSerializer {
        public static void WriteTestMovementInputGroup(this NetworkWriter writer, TestMovementInputGroup inputGroup) {}

        public static TestMovementInputGroup ReadTestMovementInputGroup(this NetworkReader reader) {
            return null;
        }
    }
}