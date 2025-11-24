"""
Bug Type: Off-by-one error
Description: Loop doesn't check the last element
Expected: Should find 9 as maximum
Actual: Returns 5 (misses the last element)
"""

def find_max(numbers):
    """Find maximum number in list"""
    if not numbers:
        return None

    max_val = numbers[0]
    # BUG: should be range(len(numbers))
    for i in range(len(numbers) - 1):
        if numbers[i] > max_val:
            max_val = numbers[i]
    return max_val

# Test case
result = find_max([1, 5, 3, 9, 2])
print(f"Maximum value: {result}")
print(f"Expected: 9, Got: {result}")
