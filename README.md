# Break the Mold
|||
-|-
Author | Jozef Porubcin

The goal of this project, Break the Mold, is to use reinforcement learning to train an AI model for close-quarters battle situations such as hostage rescue scenarios. I designed an environment in [Unity] to simulate such a scenario and I used the [Unity ML-Agents Toolkit] to train an AI model for this task. The environment supports two opposing teams of up to 12 agents each.

## Controls
1. Move: WASD
1. Aim: Cursor position
1. Shoot: Primary mouse button
1. Reload: R

## Training Setup
The Unity project can be opened in Unity 6 as-is. The following instructions are for setting up an environment for training an AI model with the Unity ML-Agents Toolkit ([release 22]).

1. Install Python 3.10.12. You can use [pyenv] to manage multiple Python versions:
    ```
    pyenv install 3.10.12
    ```
1. Set the local Python version:
    ```
    pyenv local 3.10.12
    ```
1. Create a [virtual environment] with Python 3.10.12:
    ```
    pyenv exec python -m venv .venv
    ```
1. Activate the virtual environment:

    Unix/macOS:
    ```
    source .venv/bin/activate
    ```
    Windows:
    ```
    .venv\Scripts\activate
    ```
1. Install [mlagents] 1.1.0:
    ```
    pip install mlagents==1.1.0
    ```
Now you can use [the training configuration file] to train an AI model with the Unity ML-Agents Toolkit. Replace [the default model file] to test a different model.

[Unity ML-Agents Toolkit]: https://unity-technologies.github.io/ml-agents/
[Unity]: https://unity.com
[mlagents]: https://github.com/Unity-Technologies/ml-agents/tree/develop/ml-agents
[pyenv]: https://github.com/pyenv/pyenv
[release 22]: https://github.com/Unity-Technologies/ml-agents/releases/tag/release_22
[the default model file]: Assets/Agent/AgentBehavior.onnx
[the training configuration file]: Assets/Agent/Agents.yaml
[virtual environment]: https://docs.python.org/3.10/library/venv.html#module-venv