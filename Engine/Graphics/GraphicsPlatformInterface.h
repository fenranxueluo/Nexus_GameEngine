#pragma once
#include "CommonHeaders.h"
#include "Renderer.h"

namespace nexus::graphics
{
	struct platform_interface
	{
		bool (*initialize)(void);
		void (*shutdown)(void);
	};
}
