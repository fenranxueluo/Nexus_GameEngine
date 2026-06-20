#pragma once

#define USF_STL_VECTOR 1
#define USF_STL_DEQUE 1

#if USF_STL_VECTOR
#include <iostream>
#include <algorithm>
#include <vector>
namespace nexus::utl {
	template<typename T>
	using vector = std::vector<T>;

	template<typename T> void errase_unordered(std::vector<T>& v, size_t index)
	{
		if (v.size() > 1)
		{
			std::iter_swap(v.begin() + index, v.end() - 1);
			v.pop_back();
		}
		else
		{
			v.clear();
		}
	}
}
#endif

#if USF_STL_DEQUE
#include <deque>
namespace nexus::utl {
	template<typename T>
	using deque = std::deque<T>;
}
#endif

namespace nexus::utl {

	// TODO:
}