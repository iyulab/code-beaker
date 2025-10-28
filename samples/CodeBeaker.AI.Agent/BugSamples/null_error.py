"""
Bug Type: Null/None reference error
Description: Accessing dictionary key without checking existence
Expected: Should handle missing keys gracefully
Actual: KeyError when key doesn't exist
"""

def get_user_email(user_data):
    """Get user email from data dictionary"""
    # BUG: Should check if 'email' key exists
    name = user_data.get('name', 'Unknown')
    email = user_data['email']  # This will fail if key doesn't exist

    return f"{name}: {email}"

# Test cases
user1 = {'name': 'Alice', 'email': 'alice@example.com'}
user2 = {'name': 'Bob'}  # Missing email key

print(get_user_email(user1))  # Works fine

try:
    print(get_user_email(user2))  # Will raise KeyError
except KeyError as e:
    print(f"Error: Missing key {e}")
